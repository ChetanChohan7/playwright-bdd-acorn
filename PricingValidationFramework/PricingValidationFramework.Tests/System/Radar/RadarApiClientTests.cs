using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;
using PricingValidationFramework.Core.ExternalAPIAccess.Throttling;

namespace PricingValidationFramework.Tests.System.Radar;

[TestFixture]
public class RadarApiClientTests
{
    [Test]
    public async Task PostAsync_should_return_raw_response_xml_on_success()
    {
        var handler = new StubHttpMessageHandler(
            request => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<Response><TotalAmount>100.00</TotalAmount></Response>", Encoding.UTF8, "application/xml")
            });

        using var client = new RadarApiClient(new CountingRateLimiter(), new HttpClient(handler));

        var result = await client.PostAsync(
            "Endpoint1",
            "https://placeholder-radar-1.example.com/quote?KeyName=home-abc&KeyRequestTime=2024-03-01Z09:30:45",
            "X-API-KEY",
            "secret-value",
            "<Request><TotalAmount>100.00</TotalAmount></Request>");

        Assert.That(result, Is.EqualTo("<Response><TotalAmount>100.00</TotalAmount></Response>"));
    }

        [Test]
        public async Task PostAsync_should_unwrap_json_and_normalize_utf16_xml_response()
        {
                const string xml = """
                        <?xml version="1.0" encoding="utf-16"?>
                        <underwrittenResponseResult xmlns="http://ice.com/rating/underwriting/insuranceDataModel">
                            <underwrittenResponse>
                                <underwrittenNodes>
                                    <underwrittenCoverNode description="Comprehensive">
                                        <techPrice><priceComponents><premiumPriceComponent code="premium"><calculatedAmount termAmount="19012.35" deltaAmount="19012.35" /></premiumPriceComponent></priceComponents></techPrice>
                                    </underwrittenCoverNode>
                                </underwrittenNodes>
                            </underwrittenResponse>
                        </underwrittenResponseResult>
                        """;
                var json = JsonSerializer.Serialize(new
                {
                    response = xml,
                    metadata = "ignore after XML response"
                });
                var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
                using var client = new RadarApiClient(new CountingRateLimiter(), new HttpClient(handler));

                var result = await client.PostAsync(
                        "Endpoint1",
                        "https://placeholder-radar-1.example.com/quote",
                        "X-API-KEY",
                        "secret-value",
                        "<Request />");

                Assert.Multiple(() =>
                {
                        Assert.That(result, Does.StartWith("<underwrittenResponseResult"));
                        Assert.That(result, Does.Not.Contain("<?xml"));
                        Assert.That(result, Does.Not.Contain("\\\""));
                        Assert.That(result, Does.Not.Contain("ignore after XML response"));
                        Assert.That(XDocument.Parse(result).Root!.Name.LocalName, Is.EqualTo("underwrittenResponseResult"));
                });
        }

    [Test]
    public async Task PostAsync_should_send_configured_api_key_header()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.That(request.Headers.Contains("X-API-KEY"), Is.True);
            Assert.That(request.Headers.GetValues("X-API-KEY").Single(), Is.EqualTo("secret-value"));
            Assert.That(request.Headers.GetValues("Accept").Single(), Is.EqualTo("application/json"));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<Response></Response>", Encoding.UTF8, "application/xml")
            };
        });

        using var client = new RadarApiClient(new CountingRateLimiter(), new HttpClient(handler));

        await client.PostAsync(
            "Endpoint1",
            "https://placeholder-radar-1.example.com/quote",
            "X-API-KEY",
            "secret-value",
            "<Request />");
    }

    [Test]
    public async Task PostAsync_should_send_request_xml_with_xml_content_type()
    {
        var handler = new StubHttpMessageHandler(async request =>
        {
            var contentType = request.Content!.Headers.ContentType!.MediaType;
            var body = await request.Content.ReadAsStringAsync();

            Assert.That(contentType, Is.EqualTo("application/xml"));
            Assert.That(body, Is.EqualTo("<Request><TotalAmount>100.00</TotalAmount></Request>"));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<Response />", Encoding.UTF8, "application/xml")
            };
        });

        using var client = new RadarApiClient(new CountingRateLimiter(), new HttpClient(handler));

        await client.PostAsync(
            "Endpoint1",
            "https://placeholder-radar-1.example.com/quote",
            "X-API-KEY",
            "secret-value",
            "<Request><TotalAmount>100.00</TotalAmount></Request>");
    }

    [Test]
    public void PostAsync_should_throw_readable_failure_for_unsuccessful_http_status()
    {
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("<Error />", Encoding.UTF8, "application/xml"),
            ReasonPhrase = "Internal Server Error"
        });

        var ex = Assert.ThrowsAsync<HttpRequestException>(async () =>
            await client.PostAsync(
                "Endpoint1",
                "https://placeholder-radar-1.example.com/quote",
                "X-API-KEY",
                "secret-value",
                "<Request />"));

        Assert.That(ex!.Message, Does.Contain("Radar request failed with status 500"));
        Assert.That(ex.Message, Does.Not.Contain("secret-value"));
    }

    [Test]
    public void PostAsync_should_throw_readable_failure_for_empty_response_content()
    {
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/xml")
        });

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.PostAsync(
                "Endpoint1",
                "https://placeholder-radar-1.example.com/quote",
                "X-API-KEY",
                "secret-value",
                "<Request />"));

        Assert.That(ex!.Message, Does.Contain("Radar response content is empty"));
        Assert.That(ex.Message, Does.Not.Contain("secret-value"));
    }

    [Test]
    public void PostAsync_should_preserve_cancellation_exception()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var client = CreateClient((Func<HttpRequestMessage, HttpResponseMessage>)(_ =>
            throw new InvalidOperationException("should not be called")));

        Assert.That(async () =>
            await client.PostAsync(
                "Endpoint1",
                "https://placeholder-radar-1.example.com/quote",
                "X-API-KEY",
                "secret-value",
                "<Request />",
                cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void PostAsync_should_not_expose_api_key_value_in_error_messages()
    {
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("<Error />", Encoding.UTF8, "application/xml"),
            ReasonPhrase = "Bad Request"
        });

        var ex = Assert.ThrowsAsync<HttpRequestException>(async () =>
            await client.PostAsync(
                "Endpoint1",
                "https://placeholder-radar-1.example.com/quote",
                "X-API-KEY",
                "secret-value",
                "<Request />"));

        Assert.That(ex!.Message, Does.Not.Contain("secret-value"));
    }

    [TestCase(HttpStatusCode.RequestTimeout)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.BadGateway)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    [TestCase(HttpStatusCode.GatewayTimeout)]
    public async Task PostAsync_should_retry_transient_http_statuses(HttpStatusCode transientStatus)
    {
        var callCount = 0;
        var rateLimiter = new CountingRateLimiter();
        using var client = CreateClient(_ =>
        {
            callCount++;
            return callCount == 1
                ? new HttpResponseMessage(transientStatus)
                : SuccessResponse();
        }, rateLimiter);

        var result = await client.PostAsync("Endpoint1", "https://placeholder-radar.example.com/quote", "X-API-KEY", "secret-value", "<Request />");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("<Response />"));
            Assert.That(callCount, Is.EqualTo(2));
            Assert.That(rateLimiter.AcquisitionCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task PostAsync_should_isolate_headers_and_body_for_concurrent_requests()
    {
        var requests = new ConcurrentBag<(string ApiKey, string Body)>();
        var rateLimiter = new CountingRateLimiter();
        using var client = CreateClient(async request =>
        {
            requests.Add((request.Headers.GetValues("X-API-KEY").Single(), await request.Content!.ReadAsStringAsync()));
            return SuccessResponse();
        }, rateLimiter);

        await Task.WhenAll(
            client.PostAsync("Endpoint1", "https://placeholder-radar.example.com/one", "X-API-KEY", "key-one", "<Request>one</Request>"),
            client.PostAsync("Endpoint1", "https://placeholder-radar.example.com/two", "X-API-KEY", "key-two", "<Request>two</Request>"));

        Assert.Multiple(() =>
        {
            Assert.That(requests, Does.Contain(("key-one", "<Request>one</Request>")));
            Assert.That(requests, Does.Contain(("key-two", "<Request>two</Request>")));
            Assert.That(rateLimiter.AcquisitionCount, Is.EqualTo(2));
        });
    }

    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.NotFound)]
    public void PostAsync_should_not_retry_non_transient_http_statuses(HttpStatusCode statusCode)
    {
        var callCount = 0;
        using var client = CreateClient(_ =>
        {
            callCount++;
            return new HttpResponseMessage(statusCode);
        });

        Assert.ThrowsAsync<HttpRequestException>(async () =>
            await client.PostAsync("Endpoint1", "https://placeholder-radar.example.com/quote", "X-API-KEY", "secret-value", "<Request />"));

        Assert.That(callCount, Is.EqualTo(1));
    }

    [Test]
    public async Task PostAsync_should_retry_transient_network_failures()
    {
        var callCount = 0;
        var rateLimiter = new CountingRateLimiter();
        using var client = CreateClient(_ =>
        {
            callCount++;
            if (callCount == 1)
            {
                throw new HttpRequestException("transient network failure");
            }

            return SuccessResponse();
        }, rateLimiter);

        var result = await client.PostAsync("Endpoint1", "https://placeholder-radar.example.com/quote", "X-API-KEY", "secret-value", "<Request />");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("<Response />"));
            Assert.That(callCount, Is.EqualTo(2));
            Assert.That(rateLimiter.AcquisitionCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task PostAsync_should_retry_internal_request_timeout_when_caller_token_is_not_cancelled()
    {
        var callCount = 0;
        var delays = new List<TimeSpan>();
        using var client = CreateClient(
            _ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new TaskCanceledException("request timed out");
                }

                return SuccessResponse();
            },
            retrySettings: new RetrySettings { ApiRetryCount = 1, ApiRetryDelaySeconds = 2 },
            retryDelayAsync: (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        var result = await client.PostAsync("Endpoint1", "https://placeholder-radar.example.com/quote", "X-API-KEY", "secret-value", "<Request />");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("<Response />"));
            Assert.That(callCount, Is.EqualTo(2));
            Assert.That(delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(2) }));
        });
    }

    [Test]
    public async Task PostAsync_should_stop_retries_when_cancelled()
    {
        using var cts = new CancellationTokenSource();
        var callCount = 0;
        using var client = CreateClient(_ =>
        {
            callCount++;
            cts.Cancel();
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        Assert.That(async () =>
            await client.PostAsync("Endpoint1", "https://placeholder-radar.example.com/quote", "X-API-KEY", "secret-value", "<Request />", cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(callCount, Is.EqualTo(1));
    }

    [Test]
    public async Task PostAsync_should_honor_retry_after_header()
    {
        var callCount = 0;
        using var client = CreateClient(_ =>
        {
            callCount++;
            if (callCount == 1)
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.TryAddWithoutValidation("Retry-After", "0");
                return response;
            }

            return SuccessResponse();
        });

        var result = await client.PostAsync("Endpoint1", "https://placeholder-radar.example.com/quote", "X-API-KEY", "secret-value", "<Request />");

        Assert.That(result, Is.EqualTo("<Response />"));
    }

    [Test]
    public async Task PostAsync_should_wait_for_the_larger_of_retry_after_seconds_and_local_backoff()
    {
        var callCount = 0;
        var delays = new List<TimeSpan>();
        var requests = new List<HttpRequestMessage>();
        var rateLimiter = new CountingRateLimiter();
        using var client = CreateClient(request =>
        {
            requests.Add(request);
            callCount++;
            if (callCount == 1)
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.TryAddWithoutValidation("Retry-After", "4");
                return response;
            }

            return SuccessResponse();
        }, rateLimiter, new RetrySettings { ApiRetryCount = 1, ApiRetryDelaySeconds = 2 },
        (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

        await client.PostAsync("Endpoint1", "https://placeholder-radar.example.com/quote", "X-API-KEY", "secret-value", "<Request />");

        Assert.Multiple(() =>
        {
            Assert.That(delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(4) }));
            Assert.That(rateLimiter.AcquisitionCount, Is.EqualTo(2));
            Assert.That(ReferenceEquals(requests[0], requests[1]), Is.False);
        });
    }

    [Test]
    public async Task PostAsync_should_cap_retry_after_seconds_and_log_the_effective_delay()
    {
        var callCount = 0;
        var delays = new List<TimeSpan>();
        var logger = new CapturingLogger<RadarApiClient>();
        using var client = CreateClient(_ =>
        {
            callCount++;
            if (callCount == 1)
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.TryAddWithoutValidation("Retry-After", "120");
                return response;
            }

            return SuccessResponse();
        },
        retrySettings: new RetrySettings
        {
            ApiRetryCount = 1,
            ApiRetryDelaySeconds = 2,
            ApiRetryAfterMaxDelaySeconds = 10
        },
        retryDelayAsync: (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        },
        logger: logger);

        await client.PostAsync("Endpoint1", "https://placeholder-radar.example.com/quote", "X-API-KEY", "secret-value", "<Request />");

        Assert.Multiple(() =>
        {
            Assert.That(delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(10) }));
            Assert.That(logger.Entries.Any(entry => entry.Message.Contains("SuppliedRetryAfter=120", StringComparison.Ordinal) &&
                entry.Message.Contains("ConfiguredMaximum=00:00:10", StringComparison.Ordinal) &&
                entry.Message.Contains("EffectiveDelay=00:00:10", StringComparison.Ordinal)), Is.True);
        });
    }

    [Test]
    public async Task PostAsync_should_cap_retry_after_http_date()
    {
        var callCount = 0;
        var delays = new List<TimeSpan>();
        using var client = CreateClient(_ =>
        {
            callCount++;
            if (callCount == 1)
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.TryAddWithoutValidation(
                    "Retry-After",
                    DateTimeOffset.UtcNow.AddMinutes(2).ToString("r", global::System.Globalization.CultureInfo.InvariantCulture));
                return response;
            }

            return SuccessResponse();
        },
        retrySettings: new RetrySettings
        {
            ApiRetryCount = 1,
            ApiRetryDelaySeconds = 2,
            ApiRetryAfterMaxDelaySeconds = 10
        },
        retryDelayAsync: (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

        await client.PostAsync("Endpoint1", "https://placeholder-radar.example.com/quote", "X-API-KEY", "secret-value", "<Request />");

        Assert.That(delays.Single(), Is.EqualTo(TimeSpan.FromSeconds(10)));
    }

    [Test]
    public async Task PostAsync_should_keep_local_backoff_when_it_exceeds_capped_retry_after()
    {
        var callCount = 0;
        var delays = new List<TimeSpan>();
        using var client = CreateClient(_ =>
        {
            callCount++;
            if (callCount == 1)
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.TryAddWithoutValidation("Retry-After", "120");
                return response;
            }

            return SuccessResponse();
        },
        retrySettings: new RetrySettings
        {
            ApiRetryCount = 1,
            ApiRetryDelaySeconds = 30,
            ApiRetryAfterMaxDelaySeconds = 10
        },
        retryDelayAsync: (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

        await client.PostAsync("Endpoint1", "https://placeholder-radar.example.com/quote", "X-API-KEY", "secret-value", "<Request />");

        Assert.That(delays.Single(), Is.EqualTo(TimeSpan.FromSeconds(30)));
    }

    [Test]
    public async Task PostAsync_should_warn_safely_for_invalid_retry_after()
    {
        var callCount = 0;
        var logger = new CapturingLogger<RadarApiClient>();
        using var client = CreateClient(_ =>
        {
            callCount++;
            if (callCount == 1)
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.TryAddWithoutValidation("Retry-After", "secret-invalid-header-value");
                return response;
            }

            return SuccessResponse();
        },
        retrySettings: new RetrySettings { ApiRetryCount = 1, ApiRetryDelaySeconds = 1 },
        retryDelayAsync: (_, _) => Task.CompletedTask,
        logger: logger);

        await client.PostAsync("PricingA", "https://placeholder-radar.example.com/quote", "X-API-KEY", "secret-value", "<Request />");

        Assert.Multiple(() =>
        {
            Assert.That(logger.Entries.Any(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("invalid Retry-After", StringComparison.Ordinal)), Is.True);
            Assert.That(logger.Entries.Any(entry => entry.Message.Contains("secret-invalid-header-value", StringComparison.Ordinal)), Is.False);
        });
    }

    [Test]
    public void PostAsync_should_not_send_when_endpoint_queue_rejects_a_permit()
    {
        var requestCount = 0;
        using var client = CreateClient(_ =>
        {
            requestCount++;
            return SuccessResponse();
        }, new RejectingRateLimiter());

        Assert.ThrowsAsync<RadarRequestRateLimitException>(async () =>
            await client.PostAsync("PricingA", "https://placeholder-radar.example.com/quote", "X-API-KEY", "secret-value", "<Request />"));
        Assert.That(requestCount, Is.Zero);
    }

    [Test]
    public void PostAsync_should_exhaust_429_retries_as_a_technical_http_error()
    {
        var requestCount = 0;
        var rateLimiter = new CountingRateLimiter();
        using var client = CreateClient(_ =>
        {
            requestCount++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.TryAddWithoutValidation("Retry-After", "0");
            return response;
        }, rateLimiter, new RetrySettings { ApiRetryCount = 1, ApiRetryDelaySeconds = 0 },
        (_, _) => Task.CompletedTask);

        var exception = Assert.ThrowsAsync<HttpRequestException>(async () =>
            await client.PostAsync("PricingA", "https://placeholder-radar.example.com/quote", "X-API-KEY", "secret-value", "<Request />"));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
            Assert.That(requestCount, Is.EqualTo(2));
            Assert.That(rateLimiter.AcquisitionCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task PostAsync_should_support_http_date_retry_after()
    {
        var delays = new List<TimeSpan>();
        using var client = CreateClient(_ =>
        {
            if (delays.Count == 0)
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.TryAddWithoutValidation("Retry-After", DateTimeOffset.UtcNow.AddSeconds(3).ToString("r", global::System.Globalization.CultureInfo.InvariantCulture));
                return response;
            }

            return SuccessResponse();
        }, retrySettings: new RetrySettings { ApiRetryCount = 1, ApiRetryDelaySeconds = 0 },
        retryDelayAsync: (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

        await client.PostAsync("Endpoint1", "https://placeholder-radar.example.com/quote", "X-API-KEY", "secret-value", "<Request />");

        Assert.That(delays.Single(), Is.GreaterThan(TimeSpan.FromSeconds(1)));
    }

    [TestCase(null)]
    [TestCase("not-a-retry-date")]
    public async Task PostAsync_should_use_local_backoff_when_retry_after_is_missing_or_invalid(string? retryAfter)
    {
        var callCount = 0;
        var delays = new List<TimeSpan>();
        using var client = CreateClient(_ =>
        {
            callCount++;
            if (callCount == 1)
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                if (retryAfter is not null)
                {
                    response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
                }

                return response;
            }

            return SuccessResponse();
        }, retrySettings: new RetrySettings { ApiRetryCount = 1, ApiRetryDelaySeconds = 2 },
        retryDelayAsync: (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

        await client.PostAsync("Endpoint1", "https://placeholder-radar.example.com/quote", "X-API-KEY", "secret-value", "<Request />");

        Assert.That(delays.Single(), Is.EqualTo(TimeSpan.FromSeconds(2)));
    }

    private static RadarApiClient CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory,
        IRadarRequestRateLimiter? rateLimiter = null,
        RetrySettings? retrySettings = null,
        Func<TimeSpan, CancellationToken, Task>? retryDelayAsync = null,
        ILogger<RadarApiClient>? logger = null)
    {
        return new RadarApiClient(
            rateLimiter ?? new CountingRateLimiter(),
            new HttpClient(new StubHttpMessageHandler(responseFactory)),
            logger ?? NullLogger<RadarApiClient>.Instance,
            retrySettings ?? new RetrySettings
            {
                ApiRetryCount = 2,
                ApiRetryDelaySeconds = 0
            },
            retryDelayAsync);
    }

    private static RadarApiClient CreateClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory,
        IRadarRequestRateLimiter? rateLimiter = null,
        RetrySettings? retrySettings = null,
        Func<TimeSpan, CancellationToken, Task>? retryDelayAsync = null,
        ILogger<RadarApiClient>? logger = null)
    {
        return new RadarApiClient(
            rateLimiter ?? new CountingRateLimiter(),
            new HttpClient(new StubHttpMessageHandler(responseFactory)),
            logger ?? NullLogger<RadarApiClient>.Instance,
            retrySettings ?? new RetrySettings
            {
                ApiRetryCount = 2,
                ApiRetryDelaySeconds = 0
            },
            retryDelayAsync);
    }

    private static HttpResponseMessage SuccessResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<Response />", Encoding.UTF8, "application/xml")
        };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
            : this(request => Task.FromResult(responseFactory(request)))
        {
        }

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
        {
            this.responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return responseFactory(request);
        }
    }

    private sealed class CountingRateLimiter : IRadarRequestRateLimiter
    {
        private int acquisitionCount;

        public int AcquisitionCount => Volatile.Read(ref acquisitionCount);

        public ValueTask WaitAsync(string endpointName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref acquisitionCount);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RejectingRateLimiter : IRadarRequestRateLimiter
    {
        public ValueTask WaitAsync(string endpointName, CancellationToken cancellationToken = default)
        {
            throw new RadarRequestRateLimitException(endpointName, queueRejected: true);
        }
    }

	private sealed class CapturingLogger<T> : ILogger<T>
	{
		public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			Entries.Enqueue((logLevel, formatter(state, exception)));
		}
	}

    [Test]
    public async Task PostAsync_should_wait_for_retry_delay_before_acquiring_the_next_permit()
    {
        var events = new List<string>();
        var callCount = 0;
        using var client = CreateClient(
            _ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                    response.Headers.TryAddWithoutValidation("Retry-After", "0");
                    return response;
                }

                return SuccessResponse();
            },
            new RecordingRateLimiter(events),
            new RetrySettings { ApiRetryCount = 1, ApiRetryDelaySeconds = 1 },
            (_, _) =>
            {
                events.Add("delay");
                return Task.CompletedTask;
            });

        await client.PostAsync("Endpoint1", "https://placeholder-radar.example.com/quote", "X-API-KEY", "secret-value", "<Request />");

        Assert.That(events, Is.EqualTo(new[] { "permit", "delay", "permit" }));
    }

    private sealed class RecordingRateLimiter : IRadarRequestRateLimiter
    {
        private readonly ICollection<string> events;

        public RecordingRateLimiter(ICollection<string> events)
        {
            this.events = events;
        }

        public ValueTask WaitAsync(string endpointName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            events.Add("permit");
            return ValueTask.CompletedTask;
        }
    }

	private sealed class NullScope : IDisposable
	{
		public static NullScope Instance { get; } = new();
		public void Dispose() { }
	}
}
