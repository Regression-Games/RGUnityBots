namespace RegressionGames.StateActionTypes
{
    public sealed class RGStatefulGameObject : RGStateBehaviour<RGStateEntity_Empty>
    {
        protected override RGStateEntity_Empty CreateStateEntityInstance()
        {
            return new RGStateEntity_Empty();
        }

        public override void PopulateStateEntity(RGStateEntity_Empty stateEntity)
        {
            // no-op
        }
    }
}
