namespace vaudio_godot_mono_openal;

sealed class OpenALCaptureDevice(ALCaptureDevice device) : VACaptureDevice
{
    public override void Start() => device.CaptureStart();
    public override void Stop() => device.CaptureStop();
    public override void Poll() => device.Update();
    public override void Close() => device.Close();
}
