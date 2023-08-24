using System;
using System.Collections.Generic;
using System.Text;
using VRage.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRageMath;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using VRage.Utils;

namespace WarpScrambler 
{
    // This attribute indicates that this logic component is specifically for the WarpScrambler variant of the Beacon block.
    [MyEntityComponentDescriptor(typeof(Sandbox.Common.ObjectBuilders.MyObjectBuilder_Beacon), false, new string[] { "WarpScrambler" })]
    public class WarpScramblerBlock : MyGameLogicComponent
    {
        // Constants for how long the scrambler works and how long it takes to overload and recover.
        readonly int InhibitTimer = 720; 
        private int currentInhibitTimer = 0;
        readonly int overloadTimer = 270;
        private int currentOverloadTimer = 0;
        private int maxRange = 5000;
        private bool overloaded = false;

        private VRage.ObjectBuilders.MyObjectBuilder_EntityBase _objectBuilder;
        private IMyBeacon Inhibitor;
        private IMyEntity entity;
        private bool logicEnabled = false;
        private MyParticleEffect effect;
        

        public override void Close()
        {
            // This method is empty, typically used for cleanup when the block is removed or destroyed.
        }

        public override void Init(VRage.ObjectBuilders.MyObjectBuilder_EntityBase objectBuilder)
        {
            _objectBuilder = objectBuilder;
            
            // Cast the Entity as a Beacon.
            Inhibitor = Entity as IMyBeacon;

            // Check if this Beacon has the subtype of "WarpScrambler".
            if (Inhibitor != null && Inhibitor.BlockDefinition.SubtypeId.Equals("WarpScrambler"))
            {
                logicEnabled = true;
                // Schedule this block to be updated every 10 frames.
                NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
            }
        }

        public override void UpdateAfterSimulation10()
        {
            try
            {
                // If the scrambler is overloaded or turned off, handle the overload logic.
                if (overloaded || !logicEnabled || Inhibitor == null || !Inhibitor.Enabled || !Inhibitor.IsWorking || !Inhibitor.IsFunctional)
                {
                    // If the inhibitor is turned off and not already overloaded, trigger an overload.
                    if (currentInhibitTimer > 0 && overloaded == false && !Inhibitor.Enabled)
                    {
                        MyAPIGateway.Utilities.ShowNotification("Warp Scrambler has overloaded! Cooldown in 45 seconds!", 4816);
                        currentInhibitTimer = 0;
                        currentOverloadTimer = overloadTimer;
                        overloaded = true;
                    }
                    if (overloaded)
                    {
                        // If currently overloaded, decrement the overload timer.
                        if (currentOverloadTimer > 0)
                        {
                            currentOverloadTimer--;
                            // If the player tries to enable the inhibitor during cooldown, turn it off and notify them.
                            if(Inhibitor.Enabled == true)
                            {
                                Inhibitor.Enabled = false;
                                MyAPIGateway.Utilities.ShowNotification("Warp Scrambler still Cooling down! ", 1000);
                            }
                        }
                        else
                        {
                            // If the overload timer is done, reset the overload state.
                            overloaded = false;
                            MyAPIGateway.Utilities.ShowNotification("Warp Scrambler has Cooled Down! ", 4816);
                        }
                    }
                    return;
                }

                // Increase the inhibit timer (time scrambler is active).
                currentInhibitTimer++;

                // If the scrambler has been active for too long, trigger an overload.
                if (currentInhibitTimer >= InhibitTimer)
                {
                    Inhibitor.Enabled = false;
                    MyAPIGateway.Utilities.ShowNotification("Warp Scrambler has overloaded! Cooldown in 45 seconds!", 4816);
                    overloaded = true;
                    currentInhibitTimer = 0;
                    currentOverloadTimer = overloadTimer;
                }

                // Set custom names for display.
                Inhibitor.CustomName = "Warp Scrambler";
                Inhibitor.HudText = "Warp Scrambler";

                List<IMyEntity> l = new List<IMyEntity>();

                // Create a sphere around the scrambler to find entities within its range.
                Inhibitor.Radius = Math.Min(Inhibitor.Radius, maxRange);
                BoundingSphereD sphere = new BoundingSphereD(Inhibitor.GetPosition(), Inhibitor.Radius);
                l = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);

                var parentGrid = Inhibitor.CubeGrid;
                if (entity == null)
                    entity = MyAPIGateway.Entities.GetEntityById(parentGrid.EntityId);

                if (parentGrid != null)
                {
                    // Iterate through all entities in the sphere.
                    foreach (IMyEntity e in l)
                    {
                        IMyCubeGrid grid = (e as IMyCubeGrid);

                        if (grid != null)
                        {
                            // Find and disable specific blocks like artificial mass blocks or jump drives.
                            grid.GetBlocks(null, (b) =>
                            {
                                var blockToDisable = b.FatBlock as IMyFunctionalBlock;
                                if (blockToDisable != null && (blockToDisable is IMyArtificialMassBlock || blockToDisable is IMyJumpDrive) && blockToDisable.IsWorking) {
                                    if ((b.FatBlock as Sandbox.ModAPI.Ingame.IMyFunctionalBlock).Enabled)
                                    {
                                        // Apply some damage and disable the block.
                                        var damage = grid.GridSizeEnum.Equals(MyCubeSize.Large) ? 0.5f : 0.05f;
                                        b.DecreaseMountLevel(damage, null, true);
                                        b.ApplyAccumulatedDamage();
                                        (b.FatBlock as IMyFunctionalBlock).Enabled = false;
                                    }
                                }
                                return false;
                            });
                        }
                    }
                }

                if (Inhibitor.Enabled && !overloaded)
                {
                    /*MyTransparentGeometry.AddPointBillboard(
                        MyStringId.GetOrCompute("Circle"), 
                        Color.Pink, 
                        Inhibitor.GetPosition(), 
                        (float)Inhibitor.Radius,
                        0.5f
                    );*/
                    Vector3D originalPosition = Inhibitor.GetPosition();

                    // Apply scale
                    MatrixD scalingMatrix = MatrixD.CreateScale(Inhibitor.Radius);
                    MatrixD translationMatrix = MatrixD.CreateTranslation(originalPosition);
                    MatrixD effectMatrix = scalingMatrix * translationMatrix;

                    // Reposition the effect. 
                    // If the effect is too far in one direction, you'll want to pull it back by subtracting some value.
                    Vector3D offset = new Vector3D(0, 0, 0);  // Adjusting in the negative X direction as an example. 
                    effectMatrix.Translation += offset;

                    //Damage_GravGen_Damaged too heavy
                    //Collision_Sparks_LargeGrid_Close too small
                    //Grid_Destruction nice but too much explosions
                    //Warp_Scrambler
                    MyParticleEffect effect;

                    if (MyParticlesManager.TryCreateParticleEffect("Warp_Scrambler", out effect))
                    {
                        effect.WorldMatrix = effectMatrix;
                    }
                }
            }
            catch (Exception e)
            {
                // If any error occurs, log it.
                MyAPIGateway.Utilities.ShowMessage("Warp Scrambler", "An error happened in the mod. Exception: " + e);
            }
        }

        public override VRage.ObjectBuilders.MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return _objectBuilder;
        }
    }
}
