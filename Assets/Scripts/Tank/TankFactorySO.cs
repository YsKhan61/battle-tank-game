using BTG.Entity;
using BTG.Utilities;
using UnityEngine;

namespace BTG.Tank
{
    [CreateAssetMenu(fileName = "TankFactory", menuName = "ScriptableObjects/EntityFactory/TankFactorySO")]
    public class TankFactorySO : EntityFactorySO
    {
        [SerializeField]
        TagSO m_EntityTag;

        [SerializeField]
        TankDataSO m_Data;

        private TankPool m_Pool;
        public TankPool Pool 
        { 
            get
            {
                if (m_Pool == null)
                    m_Pool = new(m_Data);
                return m_Pool;
            }
        }

        public override IEntityBrain GetEntity() => Pool.GetTank();

        public override void ReturnEntity(IEntityBrain tank) => Pool.ReturnTank(tank as TankBrain);
    }
}


