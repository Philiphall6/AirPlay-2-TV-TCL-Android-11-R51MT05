using System;
using System.Net.Http;
using System.Threading.Tasks;
using Android.Content;
using Android.Net.Nsd;

namespace AirPlay.Android.Platform;

internal sealed class DacpRemoteController : Java.Lang.Object,
    NsdManager.IDiscoveryListener,
    NsdManager.IResolveListener,
    IDisposable
{
    private const string ServiceType = "_dacp._tcp.";
    private readonly object _gate = new();
    private readonly NsdManager? _manager;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };
    private string _activeRemote = string.Empty;
    private string _dacpId = string.Empty;
    private string _host = string.Empty;
    private int _port;
    private bool _discovering;
    private bool _resolving;

    public DacpRemoteController(Context context)
    {
        _manager = context.GetSystemService(Context.NsdService) as NsdManager;
    }

    public void UpdateIdentity(string activeRemote, string dacpId)
    {
        lock (_gate)
        {
            _activeRemote = activeRemote ?? string.Empty;
            if (!string.Equals(_dacpId, dacpId, StringComparison.OrdinalIgnoreCase))
            {
                _dacpId = dacpId ?? string.Empty;
                _host = string.Empty;
                _port = 0;
            }
            if (!_discovering && !string.IsNullOrWhiteSpace(_dacpId) && _manager != null)
            {
                try
                {
                    _manager.DiscoverServices(ServiceType, NsdProtocol.DnsSd, this);
                    _discovering = true;
                }
                catch (Exception exception)
                {
                    global::Android.Util.Log.Warn("TclAirPlay", $"DACP découverte impossible: {exception.Message}");
                }
            }
        }
    }

    public async Task SendAsync(string command)
    {
        string host;
        string activeRemote;
        int port;
        lock (_gate)
        {
            host = _host;
            port = _port;
            activeRemote = _activeRemote;
        }
        if (string.IsNullOrWhiteSpace(host) || port <= 0 || string.IsNullOrWhiteSpace(activeRemote))
        {
            global::Android.Util.Log.Warn("TclAirPlay", "Commande DACP indisponible: émetteur non résolu");
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"http://{host}:{port}/ctrl-int/1/{command}");
            request.Headers.TryAddWithoutValidation("Active-Remote", activeRemote);
            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            global::Android.Util.Log.Info(
                "TclAirPlay", $"Commande DACP {command}: {(int)response.StatusCode}");
        }
        catch (Exception exception)
        {
            global::Android.Util.Log.Warn("TclAirPlay", $"Commande DACP {command} échouée: {exception.Message}");
        }
    }

    public void OnServiceFound(NsdServiceInfo? serviceInfo)
    {
        if (serviceInfo == null)
        {
            return;
        }
        lock (_gate)
        {
            if (_resolving || string.IsNullOrWhiteSpace(_dacpId) ||
                serviceInfo.ServiceName?.IndexOf(_dacpId, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }
            _resolving = true;
        }
        try
        {
            _manager?.ResolveService(serviceInfo, this);
        }
        catch (Exception exception)
        {
            lock (_gate) _resolving = false;
            global::Android.Util.Log.Warn("TclAirPlay", $"DACP résolution impossible: {exception.Message}");
        }
    }

    public void OnServiceResolved(NsdServiceInfo? serviceInfo)
    {
        lock (_gate)
        {
            _resolving = false;
            _host = serviceInfo?.Host?.HostAddress ?? string.Empty;
            _port = serviceInfo?.Port ?? 0;
        }
        global::Android.Util.Log.Info("TclAirPlay", $"DACP résolu: {_host}:{_port}");
    }

    public void OnResolveFailed(NsdServiceInfo? serviceInfo, NsdFailure errorCode)
    {
        lock (_gate) _resolving = false;
        global::Android.Util.Log.Warn("TclAirPlay", $"DACP résolution échouée: {errorCode}");
    }

    public void OnDiscoveryStarted(string? serviceType) { }
    public void OnDiscoveryStopped(string? serviceType) { lock (_gate) _discovering = false; }
    public void OnServiceLost(NsdServiceInfo? serviceInfo) { }
    public void OnStartDiscoveryFailed(string? serviceType, NsdFailure errorCode)
    {
        lock (_gate) _discovering = false;
    }
    public void OnStopDiscoveryFailed(string? serviceType, NsdFailure errorCode) { }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_gate)
            {
                if (_discovering)
                {
                    try { _manager?.StopServiceDiscovery(this); } catch (Exception) { }
                    _discovering = false;
                }
            }
            _http.Dispose();
        }
        base.Dispose(disposing);
    }
}
