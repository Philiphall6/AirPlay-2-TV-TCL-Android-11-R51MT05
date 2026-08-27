#!/system/bin/sh

# com.tcl.suspension sends an explicit broadcast to the signed TCL package.
# Keep its receiver for the system tile, but translate its log event to v10;
# BootupService stays disabled so the obsolete error 904 dialog cannot appear.
while true; do
  logcat -T 1 -v brief 'BootupReceiver:I' '*:S' 2>/dev/null |
  while IFS= read -r line; do
    case "$line" in
      *"BroadcastReceiver gets action : Show.Home.AirplayAPK"*)
        am start --user 0 -n com.philphall.tclairplayreceiver/com.philphall.tclairplayreceiver.MainActivity >/dev/null 2>&1
        ;;
    esac
  done
  sleep 2
done
