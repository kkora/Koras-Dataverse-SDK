using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Koras.Dataverse.Queries;

namespace Koras.Dataverse.Benchmarks;

/// <summary>
/// Per-request SDK overhead through the real handler chain against the canned transport
/// (docs/performance/benchmarks.md §2.6). <c>Listeners=false</c> is the headline
/// pay-for-play case proving telemetry costs nothing when nobody listens.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "BenchmarkDotNet owns the lifecycle: listeners are disposed in [GlobalCleanup], and benchmark classes cannot follow the dispose pattern because BenchmarkDotNet subclasses them.")]
public class PipelineOverheadBenchmarks
{
    private static readonly Guid CreatedId = Guid.NewGuid();

    [Params(false, true)]
    public bool Listeners { get; set; }

    private DataverseClient _client = null!;
    private Entity _row = null!;
    private ODataQuery _query = null!;
    private string _pageJson = null!;
    private ActivityListener? _activityListener;
    private MeterListener? _meterListener;

    [GlobalSetup]
    public void Setup()
    {
        _row = new Entity("account") { ["name"] = "Contoso", ["revenue"] = 250_000m };
        _query = ODataQuery.For("account").Select("name", "revenue").Top(50);

        var rows = new StringBuilder("{\"value\":[");
        for (int i = 0; i < 50; i++)
        {
            if (i > 0)
            {
                rows.Append(',');
            }

            rows.Append("{\"accountid\":\"").Append(Guid.NewGuid()).Append("\",\"name\":\"Row ").Append(i)
                .Append("\",\"revenue\":").Append(1000 + i).Append('}');
        }

        _pageJson = rows.Append("]}").ToString();

        _client = BenchmarkClient.Create(request => request.Method == HttpMethod.Post
            ? CreateResponse()
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_pageJson, Encoding.UTF8, "application/json"),
            });

        if (Listeners)
        {
            _activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Koras.Dataverse",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            };
            ActivitySource.AddActivityListener(_activityListener);

            _meterListener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == "Koras.Dataverse")
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                },
            };
            _meterListener.SetMeasurementEventCallback<long>((_, _, _, _) => { });
            _meterListener.SetMeasurementEventCallback<double>((_, _, _, _) => { });
            _meterListener.Start();
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _activityListener?.Dispose();
        _meterListener?.Dispose();
        _client.Dispose();
    }

    [Benchmark(Baseline = true)]
    public Task<Guid> CreateRoundTrip() => _client.CreateAsync(_row);

    [Benchmark]
    public Task<DataverseQueryResult> QuerySinglePage50Rows() => _client.QueryAsync(_query);

    private static HttpResponseMessage CreateResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NoContent);
        response.Headers.TryAddWithoutValidation(
            "OData-EntityId",
            $"{BenchmarkClient.EnvironmentUrl}api/data/v9.2/accounts({CreatedId:D})");
        return response;
    }
}
