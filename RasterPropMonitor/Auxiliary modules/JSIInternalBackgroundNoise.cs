using UnityEngine;

namespace JSI
{
    public class JSIInternalBackgroundNoise : InternalModule
    {
        [KSPField]
        public string soundURL;
        [KSPField]
        public float soundVolume = 0.1f;
        [KSPField]
        public bool needsElectricCharge = true;
        [KSPField]
        public string resourceName = "SYSR_ELECTRICCHARGE";
        private float electricChargeReserve;
        private FXGroup audioOutput;
        private bool isPlaying;
        private const int soundCheckRate = 60;
        private int soundCheckCountdown;
        private RasterPropMonitorComputer rpmComp;

        public void Start()
        {
            rpmComp = RasterPropMonitorComputer.Instantiate(internalProp, true);
            if (string.IsNullOrEmpty(soundURL))
            {
                JUtil.LogMessage(this, "JSIInternalBackgroundNoise called with no soundURL");
                Destroy(this);
                return;
            }

            if (needsElectricCharge)
            {
                RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
                comp.UpdateDataRefreshRate(soundCheckRate);
                electricChargeReserve = rpmComp.ProcessVariable(resourceName).MassageToFloat();
            }
            audioOutput = new FXGroup("RPM" + internalModel.internalName + vessel.id);
            audioOutput.audio = internalModel.gameObject.AddComponent<AudioSource>();
            audioOutput.audio.clip = GameDatabase.Instance.GetAudioClip(soundURL.EnforceSlashes());
            audioOutput.audio.Stop();
            audioOutput.audio.volume = GameSettings.SHIP_VOLUME * soundVolume;
            audioOutput.audio.rolloffMode = AudioRolloffMode.Logarithmic;
            audioOutput.audio.maxDistance = 10f;
            audioOutput.audio.minDistance = 8f;
            audioOutput.audio.dopplerLevel = 0f;
            audioOutput.audio.panStereo = 0f;
            audioOutput.audio.playOnAwake = false;
            audioOutput.audio.priority = 255;
            audioOutput.audio.loop = true;
            audioOutput.audio.pitch = 1f;
        }

        private void StopPlaying()
        {
            if (isPlaying)
            {
                audioOutput.audio.Stop();
                isPlaying = false;
            }
        }

        private void StartPlaying()
        {
            if (!isPlaying && (!needsElectricCharge || electricChargeReserve > 0.01f))
            {
                audioOutput.audio.Play();
                isPlaying = true;
            }
        }

        public override void OnUpdate()
        {
            if (!JUtil.UserIsInPod(part))
            {
                StopPlaying();
                return;
            }

            if (needsElectricCharge)
            {
                soundCheckCountdown--;
                if (soundCheckCountdown <= 0)
                {
                    soundCheckCountdown = soundCheckRate;
                    electricChargeReserve = rpmComp.ProcessVariable(resourceName).MassageToFloat();
                    if (electricChargeReserve < 0.01f)
                    {
                        StopPlaying();
                        return;
                    }
                }
            }

            StartPlaying();
        }
    }
}

