using BTG.Events;
using BTG.Utilities;
using BTG.Utilities.EventBus;
using UnityEngine;
using State = BTG.Actions.UltimateAction.IUltimateAction.State;


namespace BTG.Actions.UltimateAction
{
    public class Invisibility : BaseUltimateAction
    {
        public override event System.Action OnFullyCharged;

        private InvisibilityDataSO m_InvisibilityData => m_UltimateActionData as InvisibilityDataSO;

        private InvisibilityView m_View;

        public Invisibility(InvisibilityDataSO invisibilityData)
        {
            m_UltimateActionData = invisibilityData;
        }

        public override void Disable()
        {
            if (m_View != null)
            {
                Object.Destroy(m_View.gameObject);
            }

            base.Disable();
        }

        public override bool TryExecute()
        {
            if (CurrentState != State.FullyCharged)
            {
                return false;
            }

            ChangeState(State.Executing);

            SpawnView(Actor.Transform);
            m_View.PlayDisappearPS();
            m_View.PlayDisappearAudio();
            Actor.ToggleActorVisibility(false);
            RestartAfterDuration(m_InvisibilityData.Duration);

            return true;
        }

        public override void Destroy()
        {
            OnFullyCharged = null;
            base.Destroy();
        }

        protected override void Restart()
        {
            m_View.PlayAppearPS();
            m_View.PlayAppearAudio();

            _ = HelperMethods.InvokeAfterAsync((int)m_View.AppearPSDuration, () => ResetAfterDelay(), m_CTS.Token);
        }

        protected override void RaiseFullyChargedEvent()
        {
            OnFullyCharged?.Invoke();
        }

        private void SpawnView(Transform parent)
        {
            m_View = Object.Instantiate(m_InvisibilityData.InvisibilityViewPrefab, parent);
            m_View.transform.localPosition = Vector3.zero;
            m_View.transform.localRotation = Quaternion.identity;
        }

        private void ResetAfterDelay()
        {
            Object.Destroy(m_View.gameObject);
            m_View = null;
            Actor.ToggleActorVisibility(true);
            if (Actor.IsPlayer)
                EventBus<CameraShakeEvent>.Invoke(new CameraShakeEvent { ShakeAmount = 1f, ShakeDuration = 1f });

            RaiseUltimateActionExecutedEvent();

            ChangeState(State.Charging);
            Charge(-FULL_CHARGE);
            AutoCharge();
        }
    }
}
