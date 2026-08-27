using System;
using AirPlay.Android.Platform;
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
    Label = "AirPlay 2 TV TCL",
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

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
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
        SetContentView(frame);
        ReceiverStatus.Changed += OnStatusChanged;
    }

    private View BuildHeader()
    {
        var header = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal,
            Gravity = GravityFlags.CenterVertical
        };

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
            Orientation = Orientation.Horizontal,
            Gravity = GravityFlags.CenterVertical
        };
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

        var controls = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal,
            Gravity = GravityFlags.Right
        };
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

        return panel;
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
