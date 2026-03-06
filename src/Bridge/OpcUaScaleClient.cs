using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

namespace Bridge;

/// <summary>
/// Maintains a persistent OPC UA session to the scale and exposes typed
/// helpers for reading weight / stability and writing the tare command.
/// Automatically reconnects when the session drops.
/// </summary>
public sealed class OpcUaScaleClient : IOpcUaScaleClient, IAsyncDisposable
{
    private readonly OpcUaOptions _options;
    private readonly ILogger<OpcUaScaleClient> _logger;

    private ISession? _session;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private bool _disposed;

    public OpcUaScaleClient(IOptions<OpcUaOptions> options, ILogger<OpcUaScaleClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Reads the current weight value from the scale.</summary>
    public async Task<double> ReadWeightAsync(CancellationToken ct = default)
    {
        var value = await ReadNodeWithRetryAsync(_options.WeightNode, ct);
        return Convert.ToDouble(value);
    }

    /// <summary>Reads whether the scale reading is currently stable.</summary>
    public async Task<bool> IsStableAsync(CancellationToken ct = default)
    {
        var value = await ReadNodeWithRetryAsync(_options.StabilityNode, ct);
        return Convert.ToBoolean(value);
    }

    /// <summary>Writes <c>true</c> to the tare node to initiate a tare operation.</summary>
    public async Task TareAsync(CancellationToken ct = default)
    {
        var session = await EnsureSessionAsync(ct);

        var nodeId = new NodeId(_options.TareNode);
        var writeValue = new WriteValue
        {
            NodeId = nodeId,
            AttributeId = Attributes.Value,
            Value = new DataValue(new Variant(true))
        };

        var writeValues = new WriteValueCollection { writeValue };
        var response = await session.WriteAsync(null, writeValues, ct);

        ClientBase.ValidateResponse(response.Results, writeValues);

        if (response.Results[0] != StatusCodes.Good)
        {
            throw new InvalidOperationException(
                $"Tare write returned status: {response.Results[0]}");
        }

        _logger.LogInformation("Tare command written to OPC UA node {Node}", _options.TareNode);
    }

    // -------------------------------------------------------------------------
    // Session management
    // -------------------------------------------------------------------------

    private async Task<object?> ReadNodeWithRetryAsync(string nodeIdStr, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= _options.ReadRetryCount; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var session = await EnsureSessionAsync(ct);
                var nodeId = new NodeId(nodeIdStr);
                var dataValue = await session.ReadValueAsync(nodeId, ct);

                if (StatusCode.IsBad(dataValue.StatusCode))
                    throw new InvalidOperationException(
                        $"Bad status reading {nodeIdStr}: {dataValue.StatusCode}");

                _logger.LogInformation("Read {Node} = {Value}", nodeIdStr, dataValue.Value);
                return dataValue.Value;
            }
            catch (Exception ex) when (attempt < _options.ReadRetryCount)
            {
                _logger.LogWarning(ex, "Read attempt {Attempt}/{Max} failed for {Node}",
                    attempt, _options.ReadRetryCount, nodeIdStr);

                // Invalidate the session so it will be re-established next attempt.
                await InvalidateSessionAsync();
            }
        }

        // Final attempt – let the exception propagate.
        var finalSession = await EnsureSessionAsync(ct);
        var finalNodeId = new NodeId(nodeIdStr);
        var finalValue = await finalSession.ReadValueAsync(finalNodeId, ct);

        if (StatusCode.IsBad(finalValue.StatusCode))
            throw new InvalidOperationException(
                $"Bad status reading {nodeIdStr}: {finalValue.StatusCode}");

        return finalValue.Value;
    }

    private async Task<ISession> EnsureSessionAsync(CancellationToken ct)
    {
        await _sessionLock.WaitAsync(ct);
        try
        {
            if (_session is { Connected: true })
                return _session;

            _session = await ConnectAsync(ct);
            return _session;
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task InvalidateSessionAsync()
    {
        await _sessionLock.WaitAsync();
        try
        {
            if (_session != null)
            {
                try { await _session.CloseAsync(); } catch { /* best effort */ }
                _session.Dispose();
                _session = null;
            }
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task<ISession> ConnectAsync(CancellationToken ct)
    {
        _logger.LogInformation("Connecting to OPC UA server at {Url}", _options.EndpointUrl);

        var appConfig = await BuildApplicationConfigurationAsync();
        var endpointDescription = CoreClientUtils.SelectEndpoint(
            appConfig, _options.EndpointUrl, useSecurity: false);

        var endpointConfig = EndpointConfiguration.Create(appConfig);
        var endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfig);

        var session = await Session.Create(
            appConfig,
            endpoint,
            updateBeforeConnect: false,
            sessionName: "OpcUaScaleBridge",
            sessionTimeout: 60_000,
            identity: new UserIdentity(),
            preferredLocales: null,
            ct: ct);

        session.KeepAlive += OnSessionKeepAlive;

        _logger.LogInformation("OPC UA session established");
        return session;
    }

    private void OnSessionKeepAlive(ISession session, KeepAliveEventArgs e)
    {
        if (ServiceResult.IsBad(e.Status))
        {
            _logger.LogError("OPC UA keep-alive failed: {Status}. Session will be re-established.",
                e.Status);
            // Invalidate asynchronously – fire and forget is intentional here.
            _ = InvalidateSessionAsync();
        }
    }

    private static async Task<ApplicationConfiguration> BuildApplicationConfigurationAsync()
    {
        var app = new ApplicationInstance
        {
            ApplicationName = "OpcUaMtSicsBridge",
            ApplicationType = ApplicationType.Client
        };

        var config = new ApplicationConfiguration
        {
            ApplicationName = "OpcUaMtSicsBridge",
            ApplicationType = ApplicationType.Client,
            ApplicationUri = "urn:OpcUaMtSicsBridge",
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier(),
                AutoAcceptUntrustedCertificates = true,
                AddAppCertToTrustedStore = true
            },
            TransportConfigurations = new TransportConfigurationCollection(),
            TransportQuotas = new TransportQuotas { OperationTimeout = 15_000 },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60_000 },
            TraceConfiguration = new TraceConfiguration()
        };

        await config.Validate(ApplicationType.Client);

        if (app.ApplicationConfiguration == null)
            app.ApplicationConfiguration = config;

        return config;
    }

    // -------------------------------------------------------------------------
    // IAsyncDisposable
    // -------------------------------------------------------------------------

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await InvalidateSessionAsync();
        _sessionLock.Dispose();
    }
}
