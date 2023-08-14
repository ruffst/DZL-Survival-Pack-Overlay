using System;
using System.Timers;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using Sandbox.ModAPI.Interfaces.Terminal;
using ObjectBuilders.SafeZone;
using IMySafeZoneBlock = SpaceEngineers.Game.ModAPI.IMySafeZoneBlock;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game;


namespace SafeZoneCap
{

     public class Globals
    {
        public static readonly bool Debug = true;
        public static bool modificationsComplete = false;  
        public static bool PropertyChanged = true;           
        public static bool debug = false;   
        public static bool allowChangeShape = true;
        public static float maxSafeZoneRadius = 40;
        public static bool configLoaded = false;
    }


#region CONFIG

    public class Logger
    {
        private const string LOGFILE = "SafeZoneCap.log";
        
        public static void Log(string text)
        {
            if (Globals.debug) {
                try
                {
                    
                    string timestamp = DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss");
                    StringBuilder existingContent = new StringBuilder();

                    // Check if the file exists
                    if (MyAPIGateway.Utilities.FileExistsInLocalStorage(LOGFILE, typeof(Logger)))
                    {
                        // Read existing content if the file exists
                        using (var reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(LOGFILE, typeof(Logger)))
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                existingContent.AppendLine(line);
                            }
                        }
                    }
                    else
                    {
                        // Create the file if it does not exist
                        using (var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(LOGFILE, typeof(Logger)))
                        {
                            writer.Write(string.Empty);
                        }
                    }

                    // Append new content
                    existingContent.AppendLine(timestamp + ": " + text);

                    // Write combined content back to the file
                    using (var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(LOGFILE, typeof(Logger)))
                    {
                        writer.Write(existingContent.ToString());
                    }
                }
            catch (Exception ex)
                {
                    MyAPIGateway.Utilities.ShowNotification("Logger Error: " + ex.Message, 5000, MyFontEnum.Red);
                }
            }

        }
    }
    public class MyConfig
    {
        private const string FILE = "SafeZoneCap.cfg";
        

        //options

                
        public static void InitMyConfig()
        {
            if (!Load())
            {
                Save();
                Load();
                Globals.configLoaded = true;
            }
        }

        private static bool Load()
        {
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInLocalStorage(FILE, typeof(MyConfig)))
                {
                    var file = MyAPIGateway.Utilities.ReadFileInLocalStorage(FILE, typeof(MyConfig));
                    ReadSettings(file);
                    file.Close();
                    return true;
                }
            }
           catch (Exception ex)
            {
                Logger.Log("Error in Load: " + ex.Message);
            }
            return false;
        }

        private static void Save()
        {
            try
            {
                var file = MyAPIGateway.Utilities.WriteFileInLocalStorage(FILE, typeof(MyConfig));
                file.Write(GetSettingsString());
                file.Flush();
                file.Close();
            }
           catch (Exception ex)
            {
                Logger.Log("Error in Load: " + ex.Message);
            }
        }

        


        private static string GetSettingsString()
        {
            var str = new StringBuilder();

            str.Append("allow-change-shape=").Append(Globals.allowChangeShape).AppendLine();
            str.Append("max-safezone-radius=").Append(Globals.maxSafeZoneRadius).AppendLine();
            str.Append("debug=").Append(Globals.debug).AppendLine();

            return str.ToString();
        }

        private static void ReadSettings(TextReader file)
        {
            try
            {
                string line;
                string[] args;
                while ((line = file.ReadLine()) != null)
                {
                    if (line.Length == 0) continue;

                    args = line.Split(new char[] { '=' }, 2);
                    if (args.Length != 2) continue;

                    args[0] = args[0].Trim().ToLower();
                    args[1] = args[1].Trim().ToLower();

                    switch (args[0])
                    {
                        case "allow-change-shape":
                            Globals.allowChangeShape = bool.Parse(args[1]);
                            break;
                        case "max-safezone-radius":
                            Globals.maxSafeZoneRadius = float.Parse(args[1]);
                            break;
                        case "debug":
                            Globals.debug = bool.Parse(args[1]);
                            break;
                    }
                }
                Logger.Log("ReadSettings");
            }
           catch (Exception ex)
            {
                Logger.Log("Error in Load: " + ex.Message);
            }
        }
    }
 
#endregion



#region SAFEZONE CAP



    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_SafeZoneBlock), false, new string[] { "SafeZoneBlock","SafeZoneBlock2x","SafeZoneBlock4x","SafeZoneBlock8x"})]
    public class SafeZoneLogic : MyGameLogicComponent
    {
        private IMySafeZoneBlock myBlock;
        private MyObjectBuilder_EntityBase m_objectBuilder;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if (!Globals.configLoaded) MyConfig.InitMyConfig();

            base.Init(objectBuilder);
            m_objectBuilder = objectBuilder;
            myBlock = Entity as IMySafeZoneBlock;

            Logger.Log("Entity: " + (Entity != null ? Entity.ToString() : "null"));
            Logger.Log("Entity Type: " + Entity.GetType().FullName);
            
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }
        public override void UpdateOnceBeforeFrame()
        {
            if (myBlock?.CubeGrid?.Physics == null)
            {
                return; // ignore non-SafeZoneGenerators, physics-less grids and whatever other cases would cause any of those things to be null
            }

            ModifyControlsAndProperties();      // initial disabling of property and setup for UI changes

            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
        }
        public void ModifyControlsAndProperties()
        {
            // only want to run this once
            if (Globals.modificationsComplete)
            { 
                return;
            }

            Globals.modificationsComplete = true;

            MyAPIGateway.TerminalControls.CustomControlGetter -= TerminalControls_CustomControlGetter;      // in case it's already registered
            MyAPIGateway.TerminalControls.CustomControlGetter += TerminalControls_CustomControlGetter;

            ChangeValues();

            myBlock.PropertiesChanged -= OnPropertiesChanged;      // in case it's already registered
            myBlock.PropertiesChanged += OnPropertiesChanged;
        }

        static void OnPropertiesChanged(IMyTerminalBlock block)
        {
            Globals.PropertyChanged = true;
        }
        public override void UpdateBeforeSimulation()
        {
            // if something changed the property, change it back
            if (Globals.PropertyChanged)
            {
                ChangeValues();
                Globals.PropertyChanged = false;
            }
        }
        static void TerminalControls_CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            //Logger.Instance.LogDebug("TerminalControls_CustomControlGetter");

            if (block is IMySafeZoneBlock)
            {
                string subtype = (block as IMySafeZoneBlock).BlockDefinition.SubtypeId;

                IMyTerminalControl control_shape = controls.Find(x => (x.Id == "SafeZoneShapeCombo"));
                if (control_shape != null)
                {
                    if (Globals.allowChangeShape)
                    {
                        control_shape.Enabled = (b) => true; // Always enabled
                        control_shape.Visible = (b) => true; // Always visible
                    }
                    else
                    {
                        control_shape.Enabled = (b) => false; // Always disabled
                        control_shape.Visible = (b) => true; // Always visible
                    }
                }
            }
        }
        public void ChangeValues(){
            ChangeValueFloat("SafeZoneSlider", Globals.maxSafeZoneRadius);
            ChangeValueFloat("SafeZoneXSlider", Globals.maxSafeZoneRadius);
            ChangeValueFloat("SafeZoneYSlider", Globals.maxSafeZoneRadius);
            ChangeValueFloat("SafeZoneZSlider", Globals.maxSafeZoneRadius);
        }

        public void LogBlockProperties ()
        {
  
            List<ITerminalProperty> properties = new List<ITerminalProperty>();
            Func<ITerminalProperty, bool> collectAll = property => true; // Example filter function to collect all properties
            
            myBlock.GetProperties(properties, collectAll);

            // Log the properties
            foreach (var property in properties)
            {
                Logger.Log(property.Id.ToString());
            }
        }
        public void ChangeValueFloat(string propertyID, float value)
        {
            try
            {
                float currentvalue = myBlock.GetValueFloat(propertyID);
                Logger.Log("Current " + propertyID + " value:" + currentvalue);
                
                if (currentvalue > value){
                    myBlock.SetValueFloat(propertyID, value);
                    Logger.Log("SafeZone_PropertiesChanged completed for " + propertyID );
                }

            }catch (Exception ex)
            {
                Logger.Log("Exception in OnPropertiesChanged: " + ex.Message + "\n" + ex.StackTrace);
            }
        }
        public string GetPlayerNameFromIdentity(long id)
        {
            return MyVisualScriptLogicProvider.GetPlayersName(id);
        }
        public override void MarkForClose()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter -= TerminalControls_CustomControlGetter;
            myBlock.PropertiesChanged -= OnPropertiesChanged;
        }
    }


#endregion
}










