# Test Matrix — MVP Features (KDV-001..KDV-012)

> Planning document. Maps every MVP feature from
> [master plan §3](../planning/master-plan.md#3-feature-catalog-and-classification) to the
> test dimensions it must cover, with concrete example test-case names. These tests do not
> exist yet; this matrix is the contract the implementation milestones must satisfy.
> Naming convention: `MethodOrArea_Condition_ExpectedOutcome`, grouped in classes named
> `{Feature}Tests` (e.g., `RetryHandlerTests`, `ODataQueryTests`).

## Legend

| Dimension | Meaning |
|---|---|
| Happy path | Primary documented usage succeeds |
| Invalid input | Caller misuse fails fast with clear `ArgumentException`/validation errors |
| Boundary | Limits, empty sets, maximum sizes, edge encodings |
| Cancellation | Pre-canceled and mid-flight `CancellationToken` behavior |
| Failure path | Server-signaled errors map to the documented `DataverseError` taxonomy |
| Dependency failure | Underlying dependency (credential, network, handler) fails |
| Configuration | Options/DI wiring variants |
| Thread safety | Concurrent use of thread-safe components |

Every dimension below lists at least one concrete example test name; the full suites will
contain more cases per cell.

---

## KDV-001 — Authentication and token provider

| Dimension | Example test cases |
|---|---|
| Happy path | `TokenProvider_FirstCall_RequestsTokenWithEnvironmentDefaultScope`; `TokenProvider_CachedTokenValid_DoesNotCallCredential`; `AuthenticationHandler_AttachesBearerHeaderToRequest` |
| Invalid input | `UseClientSecret_EmptyTenantId_ThrowsArgumentException`; `UseCertificate_NullCertificate_ThrowsArgumentNullException` |
| Boundary | `TokenProvider_TokenExpiresInExactlyFiveMinutes_TriggersRefresh`; `TokenProvider_TokenExpiresInFiveMinutesOneSecond_UsesCachedToken` |
| Cancellation | `TokenProvider_PreCanceledToken_ThrowsOperationCanceledWithoutCredentialCall`; `TokenProvider_CancellationDuringAcquisition_PropagatesOperationCanceled` |
| Failure path | `TokenProvider_CredentialThrowsAuthenticationFailed_SurfacesDataverseExceptionWithAuthenticationCategory` |
| Dependency failure | `AuthenticationHandler_TokenProviderThrows_DoesNotSendRequest` |
| Configuration | `Options_MultipleCredentialKindsConfigured_FailsValidation`; `Options_UseTokenCredential_CustomCredentialIsUsed` |
| Thread safety | `TokenProvider_ParallelCallersDuringExpiry_SingleFlightRefreshCallsCredentialOnce` |

## KDV-002 — CRUD, upsert, alternate keys, entity model, POCO mapping

| Dimension | Example test cases |
|---|---|
| Happy path | `CreateAsync_LateBoundEntity_PostsToCollectionAndReturnsIdFromHeader`; `RetrieveAsync_WithColumnSet_SendsSelectQuery`; `UpdateAsync_SendsPatchWithIfMatchSemanticsDocumented`; `UpsertAsync_ReturnsCreatedOrUpdatedInUpsertResult`; `PocoMapping_AttributedProperties_RoundTripToEntity` |
| Invalid input | `CreateAsync_NullEntity_ThrowsArgumentNullException`; `Entity_EmptyLogicalName_ThrowsArgumentException`; `RetrieveAsync_EmptyGuid_ThrowsArgumentException` |
| Boundary | `Entity_LookupValue_SerializesAsODataBindNotAttribute`; `Entity_NullAttributeValue_SerializedAsJsonNull`; `AlternateKey_StringKeyWithQuoteAndUnicode_IsEncodedInResourcePath`; `Entity_DecimalMoneyValue_UsesInvariantCulture` |
| Cancellation | `CreateAsync_MidFlightCancellation_ThrowsOperationCanceledNotDataverseException` |
| Failure path | `RetrieveAsync_404_ThrowsDataverseExceptionWithNotFoundCategory`; `UpdateAsync_412_MapsToConcurrencyCategory` |
| Dependency failure | `CreateAsync_HandlerThrowsHttpRequestException_WrapsAsTransientNetworkError` |
| Configuration | `Client_UsesConfiguredEnvironmentUrlAndApiVersionInRequestUri` |
| Thread safety | `Client_ParallelCrudCalls_NoSharedStateCorruption` |

## KDV-003 — OData query builder, execution, `IAsyncEnumerable` auto-paging

| Dimension | Example test cases |
|---|---|
| Happy path | `ODataQuery_SelectWhereOrderBy_ProducesExpectedQueryString`; `QueryAsync_ReturnsDataverseQueryResultWithEntities`; `QueryAllAsync_FollowsNextLinkAcrossThreePages` |
| Invalid input | `ODataQuery_For_EmptyEntitySet_ThrowsArgumentException`; `Select_NullColumnName_ThrowsArgumentException` |
| Boundary | `FilterBuilder_StringValueWithSingleQuote_IsDoubledPerODataRules`; `FilterBuilder_GuidDateDecimalBoolNull_EncodeAsODataLiterals`; `QueryAllAsync_EmptyFirstPage_YieldsNothing`; `ODataQuery_TopZero_IsRejected` |
| Cancellation | `QueryAllAsync_CancellationBetweenPages_StopsWithoutFetchingNextPage`; `QueryAllAsync_EnumeratorDisposedEarly_NoFurtherRequests` |
| Failure path | `QueryAsync_400InvalidQuery_MapsToInvalidRequestCategoryWithServerMessage` |
| Dependency failure | `QueryAllAsync_SecondPage429ThenSuccess_RetriesAndContinuesEnumeration` |
| Configuration | `QueryAsync_MaxPageSizeOption_SendsPreferODataMaxPageSize` |
| Thread safety | `ODataQuery_DocumentedAsNotThreadSafe_NoConcurrentMutationTestPretendsOtherwise` (doc-assert: builder instances are per-use; test verifies `Build`-style immutability of produced query string once executed) |

## KDV-004 — FetchXML builder, execution, paging cookies

| Dimension | Example test cases |
|---|---|
| Happy path | `FetchXml_AttributesFilterLinkOrderTop_ProducesExpectedXml`; `FetchAsync_SendsFetchXmlAsQueryParameter`; `FetchAllAsync_UsesPagingCookieForSubsequentPages` |
| Invalid input | `FetchXml_For_EmptyEntityName_ThrowsArgumentException`; `Link_EmptyFromAttribute_ThrowsArgumentException` |
| Boundary | `Filter_ValueWithAngleBracketsAmpersandQuotes_IsXmlEscaped`; `FetchXml_NestedFiltersAndOrCombination_ProducesCorrectFilterTree`; `FetchXml_Top5000Boundary_Accepted`; `PagingCookie_SpecialCharacters_RoundTripEncoded` |
| Cancellation | `FetchAllAsync_CancellationBetweenPages_ThrowsOperationCanceled` |
| Failure path | `FetchAsync_ServerRejectsFetchXml_MapsToInvalidRequestCategory` |
| Dependency failure | `FetchAsync_TransientFailureThenSuccess_ReturnsPage` |
| Configuration | `FetchXmlPackage_HasZeroThirdPartyDependencies` (architecture test); netstandard2.0 TFM compile test in package-consumption suite |
| Thread safety | Builder documented not thread-safe; produced XML string is immutable — `Build_CalledTwice_ReturnsEquivalentXml` |

## KDV-005 — Batch, change sets, continue-on-error

| Dimension | Example test cases |
|---|---|
| Happy path | `BatchRequest_MixedOperations_GeneratesValidMultipartPayload`; `ExecuteBatchAsync_ParsesPerItemResultsInOrder`; `ChangeSet_AtomicOperations_ShareChangeSetBoundary` |
| Invalid input | `BatchRequest_ZeroOperations_ThrowsArgumentException`; `ChangeSet_GetOperationInsideChangeSet_Rejected` |
| Boundary | `BatchRequest_Exactly1000Operations_Accepted`; `BatchRequest_1001Operations_ThrowsBeforeSending`; `Batch_ContentIdReferencesBetweenOperations_SerializedCorrectly` |
| Cancellation | `ExecuteBatchAsync_MidFlightCancellation_ThrowsOperationCanceled` |
| Failure path | `ExecuteBatchAsync_ChangeSetFailure_AllItemsReportFailedAtomically`; `ExecuteBatchAsync_ContinueOnError_ReturnsMixedItemResultsWithoutThrowing`; `ExecuteBatchAsync_ItemErrorPayload_MapsToDataverseErrorPerItem` |
| Dependency failure | `ExecuteBatchAsync_WholeBatch429_RetriesEntireBatch` |
| Configuration | `Batch_UsesConfiguredApiVersionInInnerRequestUris` |
| Thread safety | `Client_ParallelBatchExecutions_IndependentResults` |

## KDV-006 — Metadata client

| Dimension | Example test cases |
|---|---|
| Happy path | `GetTableAsync_ReturnsTableMetadataWithLogicalAndSchemaNames`; `GetColumnsAsync_MapsColumnTypesToTypedModels`; `GetGlobalChoiceAsync_ReturnsChoiceOptionsWithLabels`; `GetRelationshipsAsync_ReturnsOneToManyAndManyToMany` |
| Invalid input | `GetTableAsync_EmptyLogicalName_ThrowsArgumentException` |
| Boundary | `GetColumnsAsync_TableWithNoCustomColumns_ReturnsSystemColumnsOnly`; `ChoiceOption_MissingLabelForLanguage_FallsBackDocumentedly` |
| Cancellation | `GetTableAsync_PreCanceled_ThrowsOperationCanceled` |
| Failure path | `GetTableAsync_UnknownTable_MapsToNotFoundCategory` |
| Dependency failure | `MetadataClient_TransientFailure_RetriesPerPolicy` |
| Configuration | `AddDataverse_RegistersIMetadataClientAsSingleton` |
| Thread safety | `MetadataClient_ParallelReads_Safe` |

## KDV-007 — Solution client

| Dimension | Example test cases |
|---|---|
| Happy path | `ExportSolutionAsync_ReturnsSolutionBytes`; `ImportSolutionAsync_PollsAsyncJobUntilCompleted`; `PublishAllAsync_SendsPublishAllXmlRequest`; `GetInstalledSolutionsAsync_ReturnsSolutionInfoList` |
| Invalid input | `ExportSolutionAsync_EmptySolutionName_ThrowsArgumentException`; `ImportSolutionAsync_EmptyPayload_ThrowsArgumentException` |
| Boundary | `ImportSolutionAsync_JobCompletesOnFirstPoll_NoExtraPolling`; `ImportSolutionAsync_PollingIntervalRespectsOptionsAndTimeProvider` |
| Cancellation | `ImportSolutionAsync_CancellationDuringPolling_ThrowsOperationCanceledAndStopsPolling` |
| Failure path | `ImportSolutionAsync_JobFaulted_ThrowsDataverseExceptionWithJobErrorDetail` |
| Dependency failure | `ExportSolutionAsync_503WithRetryAfter_RetriesThenSucceeds` |
| Configuration | `AddDataverse_RegistersISolutionClient` |
| Thread safety | `SolutionClient_ConcurrentQueries_Safe` |

## KDV-008 — Resilience: retry, throttling, timeout, backoff

| Dimension | Example test cases |
|---|---|
| Happy path | `RetryHandler_TransientStatusThenSuccess_ReturnsSuccessTransparently` |
| Invalid input | `RetryOptions_NegativeMaxRetries_FailsValidation`; `RetryOptions_BackoffCeilingBelowBase_FailsValidation` |
| Boundary | `RetryHandler_ExactlyMaxRetriesFailures_ThrowsAfterLastAttempt`; `RetryAfter_ZeroSeconds_RetriesImmediately`; `RetryAfter_HttpDateFormat_ComputedAgainstTimeProviderNow` |
| Cancellation | `RetryHandler_CancellationDuringBackoffDelay_AbortsImmediately` |
| Failure path | `RetryHandler_429WithRetryAfter_DelaysExactlyHeaderValue`; `RetryHandler_400_NeverRetries`; `RetryHandler_ExhaustedRetries_SurfacesLastErrorAsTransient` |
| Dependency failure | `RetryHandler_HttpRequestException_TreatedAsTransientAndRetried`; `RetryHandler_TimeoutPerAttempt_RetriesNextAttempt` |
| Configuration | `RetryOptions_Disabled_PassesFailuresThroughWithoutDelay`; `RetryOptions_CustomJitterBounds_DelaysWithinBounds` |
| Thread safety | `RetryHandler_ConcurrentRequests_IndependentRetryStateNoCrossTalk` |

## KDV-009 — Error model

| Dimension | Example test cases |
|---|---|
| Happy path | `ErrorParser_StandardODataErrorPayload_MapsCodeMessageAndRequestId` |
| Invalid input | (not applicable — parser takes server payloads; hostile payloads under Boundary/Failure) |
| Boundary | `ErrorParser_EmptyBody_ProducesFallbackErrorWithHttpStatus`; `ErrorParser_NonJsonHtmlBody_DoesNotThrow`; `ErrorParser_DeeplyNestedInnerError_ExtractedWithoutRecursionIssues`; `ErrorParser_VeryLargeErrorBody_TruncatedSafely` |
| Cancellation | (not applicable — pure parsing) |
| Failure path | `ErrorParser_ServiceProtectionCodeOn429_TransientTrueThrottlingCategory`; `ErrorParser_403_PermissionCategoryTransientFalse`; `ErrorParser_412_ConcurrencyCategory`; `DataverseException_Message_ContainsCategoryStatusAndRequestId` |
| Dependency failure | (covered via KDV-008 integration of mapping and retries) |
| Configuration | (not applicable) |
| Thread safety | `ErrorParser_Stateless_ParallelParsesIndependent` |

## KDV-010 — DI, options, named clients, factory, startup validation

| Dimension | Example test cases |
|---|---|
| Happy path | `AddDataverse_ResolvesIDataverseClientSingleton`; `Factory_CreateClient_ByName_ReturnsClientWithThatConfiguration` |
| Invalid input | `AddDataverse_NullConfigureAction_ThrowsArgumentNullException`; `Factory_CreateClient_UnknownName_ThrowsMeaningfulException` |
| Boundary | `AddDataverse_CalledTwiceSameName_LastConfigurationWinsOrThrowsAsDocumented`; `NamedClients_TwoNames_IndependentOptionsAndHttpClients` |
| Cancellation | (not applicable — registration is synchronous) |
| Failure path | `StartupValidation_MissingEnvironmentUrl_FailsOnHostStartNotFirstCall` |
| Dependency failure | `AddDataverse_WithoutLoggingRegistered_StillResolves` (NullLogger fallback via abstractions) |
| Configuration | `Options_BindFromConfigurationSection_MatchesDocumentedAppsettingsShape`; `Options_HttpUrl_FailsDataAnnotationsValidation` |
| Thread safety | `Factory_ParallelCreateClientSameName_ReturnsSameSingletonInstance` |

## KDV-011 — Observability: logging, ActivitySource, Meter

| Dimension | Example test cases |
|---|---|
| Happy path | `Client_Operation_EmitsActivityFromKorasDataverseSourceWithOperationName`; `Meter_RequestCounterAndDurationHistogram_RecordedPerOperation`; `Logging_UsesDocumentedCategoryNames` |
| Invalid input | (not applicable) |
| Boundary | `Activity_NoListener_ZeroAllocationPathDoesNotThrow`; `Activity_WrapsAllRetryAttemptsInSingleActivity` |
| Cancellation | `Activity_CanceledOperation_SetsActivityStatusWithoutLeakingToken` |
| Failure path | `Activity_FailedOperation_RecordsErrorCategoryTagNotRawPayload`; `Logging_Failure_NeverLogsAuthorizationHeaderOrTokens` |
| Dependency failure | `OTelPackage_TracerProviderBuilderExtension_SubscribesToKorasDataverseSource` |
| Configuration | `CorePackage_HasNoOpenTelemetryDependency` (architecture test) |
| Thread safety | `Meter_ConcurrentRecordings_NoException` |

## KDV-012 — Health checks (WhoAmI probe)

| Dimension | Example test cases |
|---|---|
| Happy path | `HealthCheck_WhoAmISucceeds_ReturnsHealthyWithLatencyData` |
| Invalid input | `AddDataverseHealthCheck_BeforeAddDataverse_ThrowsMeaningfulException` |
| Boundary | `HealthCheck_SlowResponseWithinTimeout_StillHealthy` |
| Cancellation | `HealthCheck_CanceledContextToken_ThrowsOperationCanceledPerHealthCheckContract` |
| Failure path | `HealthCheck_401_ReturnsUnhealthyWithAuthenticationCategoryNoSecretsInDescription`; `HealthCheck_429_ReturnsDegradedOrUnhealthyAsDocumented` |
| Dependency failure | `HealthCheck_NetworkFailure_ReturnsUnhealthyNotThrow` |
| Configuration | `AddDataverseHealthCheck_NamedClient_ProbesThatClient`; `AddDataverseHealthCheck_CustomNameAndTags_RegisteredOnHealthCheckService` |
| Thread safety | `HealthCheck_ParallelExecutions_Safe` |

---

## Cross-cutting suites (not per-feature)

| Suite | Scope |
|---|---|
| Architecture (`ArchitectureTests`) | Dependency direction, sealed/abstract rule, Async suffix + CancellationToken rule, zero third-party deps in Abstractions/FetchXml, namespace layout |
| Serialization invariance | Culture matrix (`en-US`, `de-DE`, `tr-TR`) across all value serialization tests |
| Security corpus | Hostile-input encoding corpus applied to every builder value position (see [test-strategy.md §4.14](test-strategy.md)) |
| Public API | `PublicAPI.Unshipped.txt` diffs reviewed per PR |
| Package consumption | Per-TFM compile+run of packed output ([compatibility-testing.md](compatibility-testing.md)) |
| Live integration | KDV-001..KDV-012 round trips against a real environment ([integration-testing.md](integration-testing.md)) |
