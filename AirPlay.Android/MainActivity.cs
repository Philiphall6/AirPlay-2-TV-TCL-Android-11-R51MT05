using System;
using System.Linq;
using AirPlay.Android.Platform;
using AirPlay.Models;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace AirPlay.Android;

[Activity(
    Name = "com.philphall.tclairplayreceiver.MainActivity",
    Label = "AirPlay 2 TV TCL",
    Theme = "@android:style/Theme.Material.NoActionBar",
    MainLauncher = true,
    Exported = true,
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class MainActivity : Activity, ISurfaceHolderCallback
{
    private static readonly Color AppleBlue = Color.ParseColor("#0A84FF");
    private static readonly Color PrimaryText = Color.ParseColor("#F5F5F7");
    private static readonly Color SecondaryText = Color.ParseColor("#A1A1A6");
    private static readonly Color PanelColor = Color.ParseColor("#1C1C1E");
    private static readonly Color FieldColor = Color.ParseColor("#2C2C2E");
    private static readonly Color BorderColor = Color.ParseColor("#3A3A3C");

    private TextView? _status;
    private TextView? _namePreview;
    private EditText? _receiverName;
    private Handler? _restartHandler;
    private LinearLayout? _chrome;
    private LinearLayout? _audioScreen;
    private ImageView? _artwork;
    private TextView? _trackTitle;
    private TextView? _trackDetails;
    private TextView? _playPauseLabel;
    private ProgressBar? _trackProgress;
    private CheckBox? _audioScreenToggle;
    private NowPlayingInfo _nowPlaying = new();

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.SetSoftInputMode(SoftInput.StateAlwaysHidden);
        _restartHandler = new Handler(Looper.MainLooper!);

        var frame = new FrameLayout(this);
        var surface = new SurfaceView(this);
        surface.Holder?.AddCallback(this);
        surface.SetBackgroundColor(Color.Black);
        frame.AddView(surface, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent));

        _chrome = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        _chrome.SetPadding(Dp(52), Dp(34), Dp(52), Dp(28));
        _chrome.SetBackgroundColor(Color.Black);

        _chrome.AddView(BuildHeader());
        _chrome.AddView(BuildConfigurationPanel(), new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent)
        {
            TopMargin = Dp(24),
            BottomMargin = Dp(22)
        });

        frame.AddView(_chrome, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent));
        _audioScreen = BuildNowPlayingScreen();
        _audioScreen.Visibility = ViewStates.Gone;
        frame.AddView(_audioScreen, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent));
        SetContentView(frame);
        ReceiverStatus.Changed += OnStatusChanged;
        NowPlayingStatus.Changed += OnNowPlayingChanged;
        ApplyNowPlaying(NowPlayingStatus.Current);
    }

    private View BuildHeader()
    {
        var header = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };
        header.SetGravity(GravityFlags.CenterVertical);

        var logo = new ImageView(this)
        {
            ContentDescription = "Logo AirPlay"
        };
        logo.SetImageResource(Resource.Drawable.airplay_logo);
        header.AddView(logo, new LinearLayout.LayoutParams(Dp(82), Dp(82))
        {
            RightMargin = Dp(24)
        });

        var titles = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        var title = CreateText("AirPlay 2", 30f, PrimaryText, true);
        var subtitle = CreateText("TV TCL Android 11 · R51MT05", 16f, SecondaryText, false);
        _status = CreateText(ReceiverStatus.Current, 14f, AppleBlue, true);

        titles.AddView(title);
        titles.AddView(subtitle, WithTopMargin(Dp(3)));
        titles.AddView(_status, WithTopMargin(Dp(8)));
        header.AddView(titles);
        return header;
    }

    private View BuildConfigurationPanel()
    {
        var panel = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            Elevation = Dp(10)
        };
        panel.SetPadding(Dp(28), Dp(22), Dp(28), Dp(24));
        panel.Background = RoundedBackground(PanelColor, Dp(24), BorderColor, 1);

        panel.AddView(CreateText("NOM DU RÉCEPTEUR", 12f, SecondaryText, true));

        var nameRow = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };
        nameRow.SetGravity(GravityFlags.CenterVertical);
        var baseName = ReceiverNameSettings.GetBaseName(this);
        _receiverName = new EditText(this)
        {
            Text = baseName,
            TextSize = 18f,
            Hint = ReceiverNameSettings.DefaultBaseName
        };
        _receiverName.SetSingleLine(true);
        _receiverName.SetTextColor(PrimaryText);
        _receiverName.SetHintTextColor(SecondaryText);
        _receiverName.SetPadding(Dp(18), 0, Dp(18), 0);
        _receiverName.Background = FocusableBackground(FieldColor, BorderColor, AppleBlue, Dp(14));
        _receiverName.TextChanged += (_, _) => UpdateNamePreview();
        nameRow.AddView(_receiverName, new LinearLayout.LayoutParams(0, Dp(58), 1f)
        {
            RightMargin = Dp(14)
        });

        var save = CreateButton("Enregistrer et appliquer", true);
        save.Click += (_, _) => SaveAndRestartReceiver();
        nameRow.AddView(save, new LinearLayout.LayoutParams(Dp(250), Dp(58)));
        panel.AddView(nameRow, MatchWidthWithTopMargin(Dp(10)));

        panel.AddView(CreateText("Les suffixes Audio et Video restent fixes.", 13f, SecondaryText, false),
            WithTopMargin(Dp(12)));

        _namePreview = CreateText(string.Empty, 16f, PrimaryText, true);
        _namePreview.SetPadding(Dp(18), Dp(12), Dp(18), Dp(12));
        _namePreview.Background = RoundedBackground(FieldColor, Dp(14), BorderColor, 1);
        panel.AddView(_namePreview, MatchWidthWithTopMargin(Dp(12)));
        UpdateNamePreview();

        _audioScreenToggle = new CheckBox(this)
        {
            Text = "Afficher la pochette et les commandes pendant la lecture audio",
            TextSize = 14f,
            Checked = AudioScreenSettings.IsEnabled(this)
        };
        _audioScreenToggle.SetTextColor(PrimaryText);
        _audioScreenToggle.ButtonTintList = global::Android.Content.Res.ColorStateList.ValueOf(AppleBlue);
        _audioScreenToggle.CheckedChange += (_, args) =>
        {
            AudioScreenSettings.Save(this, args.IsChecked);
            ApplyNowPlaying(_nowPlaying);
        };
        panel.AddView(_audioScreenToggle, MatchWidthWithTopMargin(Dp(14)));

        var controls = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };
        controls.SetGravity(GravityFlags.Right);
        var start = CreateButton("Démarrer AirPlay", true);
        var stop = CreateButton("Arrêter", false);
        start.Click += (_, _) => StartReceiver();
        stop.Click += (_, _) => StopService(new Intent(this, typeof(AirPlayForegroundService)));
        controls.AddView(start, new LinearLayout.LayoutParams(Dp(210), Dp(54))
        {
            RightMargin = Dp(12)
        });
        controls.AddView(stop, new LinearLayout.LayoutParams(Dp(140), Dp(54)));
        panel.AddView(controls, MatchWidthWithTopMargin(Dp(18)));
        start.RequestFocus();

        return panel;
    }

    private LinearLayout BuildNowPlayingScreen()
    {
        var screen = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };
        screen.SetGravity(GravityFlags.Center);
        screen.SetPadding(Dp(110), Dp(70), Dp(110), Dp(70));
        screen.SetBackgroundColor(Color.Black);

        _artwork = new ImageView(this)
        {
            ContentDescription = "Pochette de l’album"
        };
        _artwork.SetScaleType(ImageView.ScaleType.CenterCrop);
        _artwork.SetImageResource(Resource.Drawable.app_icon);
        _artwork.Background = RoundedBackground(FieldColor, Dp(20), BorderColor, 1);
        screen.AddView(_artwork, new LinearLayout.LayoutParams(Dp(390), Dp(390))
        {
            RightMargin = Dp(70)
        });

        var information = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        information.SetGravity(GravityFlags.CenterVertical);
        _trackTitle = CreateText("Lecture AirPlay", 30f, PrimaryText, true);
        _trackTitle.SetMaxLines(2);
        _trackDetails = CreateText("En attente des métadonnées…", 18f, SecondaryText, false);
        _trackDetails.SetMaxLines(2);
        information.AddView(CreateText("AIRPLAY AUDIO", 12f, AppleBlue, true));
        information.AddView(_trackTitle, WithTopMargin(Dp(12)));
        information.AddView(_trackDetails, WithTopMargin(Dp(8)));

        _trackProgress = new ProgressBar(this, null, global::Android.Resource.Attribute.ProgressBarStyleHorizontal)
        {
            Max = 1000,
            Progress = 0
        };
        information.AddView(_trackProgress, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, Dp(8))
        {
            TopMargin = Dp(28)
        });

        var controls = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };
        controls.SetGravity(GravityFlags.CenterVertical);
        var previous = CreateButton("◀◀", false);
        var playPause = CreateButton("▶", true);
        var next = CreateButton("▶▶", false);
        _playPauseLabel = playPause;
        previous.ContentDescription = "Morceau précédent";
        playPause.ContentDescription = "Lecture ou pause";
        next.ContentDescription = "Morceau suivant";
        previous.Click += (_, _) => SendMediaCommand(AirPlayForegroundService.ActionPrevious);
        playPause.Click += (_, _) => SendMediaCommand(AirPlayForegroundService.ActionPlayPause);
        next.Click += (_, _) => SendMediaCommand(AirPlayForegroundService.ActionNext);
        controls.AddView(previous, new LinearLayout.LayoutParams(Dp(110), Dp(62)));
        controls.AddView(playPause, new LinearLayout.LayoutParams(Dp(128), Dp(68))
        {
            LeftMargin = Dp(18),
            RightMargin = Dp(18)
        });
        controls.AddView(next, new LinearLayout.LayoutParams(Dp(110), Dp(62)));
        information.AddView(controls, WithTopMargin(Dp(30)));
        screen.AddView(information, new LinearLayout.LayoutParams(0,
            ViewGroup.LayoutParams.WrapContent, 1f));
        return screen;
    }

    private void SendMediaCommand(string action)
    {
        var intent = new Intent(this, typeof(AirPlayForegroundService));
        intent.SetAction(action);
        StartForegroundService(intent);
    }

    private void OnNowPlayingChanged(object? sender, NowPlayingInfo value) =>
        RunOnUiThread(() => ApplyNowPlaying(value));

    private void ApplyNowPlaying(NowPlayingInfo value)
    {
        _nowPlaying = value.Clone();
        if (_trackTitle != null)
        {
            _trackTitle.Text = string.IsNullOrWhiteSpace(value.Title) ? "Lecture AirPlay" : value.Title;
        }
        if (_trackDetails != null)
        {
            var details = string.Join(" · ", new[] { value.Artist, value.Album }
                .Where(item => !string.IsNullOrWhiteSpace(item)));
            _trackDetails.Text = string.IsNullOrWhiteSpace(details)
                ? "En attente des métadonnées…"
                : details;
        }
        if (_playPauseLabel != null)
        {
            _playPauseLabel.Text = value.IsPlaying ? "Ⅱ" : "▶";
        }
        if (_trackProgress != null)
        {
            var duration = value.ProgressEnd - value.ProgressStart;
            var elapsed = value.ProgressCurrent - value.ProgressStart;
            _trackProgress.Progress = duration > 0
                ? (int)Math.Clamp(elapsed * 1000L / duration, 0L, 1000L)
                : 0;
        }
        if (_artwork != null)
        {
            if (value.Artwork?.Length > 0)
            {
                var bitmap = BitmapFactory.DecodeByteArray(value.Artwork, 0, value.Artwork.Length);
                _artwork.SetImageBitmap(bitmap);
            }
            else
            {
                _artwork.SetImageResource(Resource.Drawable.app_icon);
            }
        }

        var showAudio = AudioScreenSettings.IsEnabled(this) &&
            (value.IsPlaying || !string.IsNullOrWhiteSpace(value.Title) || value.Artwork?.Length > 0);
        if (_audioScreen != null)
        {
            _audioScreen.Visibility = showAudio ? ViewStates.Visible : ViewStates.Gone;
        }
        if (_chrome != null && !ReceiverStatus.Current.StartsWith("Décodage H.264", StringComparison.Ordinal))
        {
            _chrome.Visibility = showAudio ? ViewStates.Gone : ViewStates.Visible;
        }
    }

    private void SaveAndRestartReceiver()
    {
        var baseName = ReceiverNameSettings.SaveBaseName(this, _receiverName?.Text);
        if (_receiverName != null)
        {
            _receiverName.Text = baseName;
            _receiverName.SetSelection(baseName.Length);
        }
        UpdateNamePreview();

        ReceiverStatus.Publish("Nom enregistré · redémarrage AirPlay…");
        StopService(new Intent(this, typeof(AirPlayForegroundService)));
        _restartHandler?.PostDelayed(StartReceiver, 900);
    }

    private void UpdateNamePreview()
    {
        if (_namePreview == null)
        {
            return;
        }

        var baseName = string.IsNullOrWhiteSpace(_receiverName?.Text)
            ? ReceiverNameSettings.DefaultBaseName
            : _receiverName!.Text!;
        _namePreview.Text =
            $"♫  {ReceiverNameSettings.AudioName(baseName)}     ▸  {ReceiverNameSettings.VideoName(baseName)}";
    }

    private void StartReceiver()
    {
        var intent = new Intent(this, typeof(AirPlayForegroundService));
        StartForegroundService(intent);
    }

    private void OnStatusChanged(object? sender, string value)
    {
        RunOnUiThread(() =>
        {
            if (_status != null)
            {
                _status.Text = value;
            }
            if (value.StartsWith("Décodage H.264", StringComparison.Ordinal))
            {
                if (_chrome != null)
                {
                    _chrome.Visibility = ViewStates.Gone;
                }
                if (_audioScreen != null)
                {
                    _audioScreen.Visibility = ViewStates.Gone;
                }
            }
        });
    }

    public override bool OnKeyDown(Keycode keyCode, KeyEvent? e)
    {
        if (_chrome?.Visibility == ViewStates.Gone)
        {
            _chrome.Visibility = ViewStates.Visible;
            return true;
        }

        return base.OnKeyDown(keyCode, e);
    }

    private TextView CreateText(string text, float size, Color color, bool medium)
    {
        var view = new TextView(this)
        {
            Text = text,
            TextSize = size,
            Typeface = Typeface.Create("sans-serif", medium ? TypefaceStyle.Bold : TypefaceStyle.Normal)
        };
        view.SetTextColor(color);
        return view;
    }

    private Button CreateButton(string text, bool primary)
    {
        var button = new Button(this)
        {
            Text = text,
            TextSize = 15f,
            Typeface = Typeface.Create("sans-serif", TypefaceStyle.Bold)
        };
        button.SetAllCaps(false);
        button.SetTextColor(primary ? Color.White : PrimaryText);
        button.Background = FocusableBackground(
            primary ? AppleBlue : FieldColor,
            primary ? AppleBlue : BorderColor,
            Color.White,
            Dp(14));
        return button;
    }

    private StateListDrawable FocusableBackground(Color normal, Color border, Color focused, int radius)
    {
        var states = new StateListDrawable();
        states.AddState(
            new[] { global::Android.Resource.Attribute.StateFocused },
            RoundedBackground(normal, radius, focused, 3));
        states.AddState(
            new[] { global::Android.Resource.Attribute.StatePressed },
            RoundedBackground(normal, radius, focused, 3));
        states.AddState(Array.Empty<int>(), RoundedBackground(normal, radius, border, 1));
        return states;
    }

    private GradientDrawable RoundedBackground(Color fill, int radius, Color stroke, int strokeWidth)
    {
        var drawable = new GradientDrawable();
        drawable.SetColor(fill);
        drawable.SetCornerRadius(radius);
        drawable.SetStroke(Dp(strokeWidth), stroke);
        return drawable;
    }

    private LinearLayout.LayoutParams WithTopMargin(int margin) => new(
        ViewGroup.LayoutParams.WrapContent,
        ViewGroup.LayoutParams.WrapContent)
    {
        TopMargin = margin
    };

    private LinearLayout.LayoutParams MatchWidthWithTopMargin(int margin) => new(
        ViewGroup.LayoutParams.MatchParent,
        ViewGroup.LayoutParams.WrapContent)
    {
        TopMargin = margin
    };

    private int Dp(int value) => (int)TypedValue.ApplyDimension(
        ComplexUnitType.Dip,
        value,
        Resources?.DisplayMetrics);

    protected override void OnDestroy()
    {
        ReceiverStatus.Changed -= OnStatusChanged;
        NowPlayingStatus.Changed -= OnNowPlayingChanged;
        ReceiverSurfaceRegistry.Set(null);
        _restartHandler?.RemoveCallbacksAndMessages(null);
        _restartHandler?.Dispose();
        _restartHandler = null;
        base.OnDestroy();
    }

    public void SurfaceCreated(ISurfaceHolder holder) => ReceiverSurfaceRegistry.Set(holder.Surface);

    public void SurfaceChanged(ISurfaceHolder holder, Format format, int width, int height) =>
        ReceiverSurfaceRegistry.Set(holder.Surface);

    public void SurfaceDestroyed(ISurfaceHolder holder) => ReceiverSurfaceRegistry.Set(null);
}
