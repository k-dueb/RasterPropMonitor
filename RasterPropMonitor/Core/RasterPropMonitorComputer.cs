/*****************************************************************************
 * RasterPropMonitor
 * =================
 * Plugin for Kerbal Space Program
 *
 *  by Mihara (Eugene Medvedev), MOARdV, and other contributors
 * 
 * RasterPropMonitor is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, revision
 * date 29 June 2007, or (at your option) any later version.
 * 
 * RasterPropMonitor is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License
 * for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with RasterPropMonitor.  If not, see <http://www.gnu.org/licenses/>.
 ****************************************************************************/
using System;
using System.Collections.Generic;
using UnityEngine;

namespace JSI
{
    public partial class RasterPropMonitorComputer : PartModule
    {
        // The only public configuration variable.
        [KSPField]
        public string storedStrings = string.Empty;

        // The OTHER public configuration variable.
        [KSPField]
        public string triggeredEvents = string.Empty;

        // Yes, it's a really braindead way of doing it, but I ran out of elegant ones,
        // because nothing appears to work as documented -- IF it's documented.
        // This one is sure to work and isn't THAT much of a performance drain, really.
        // Pull requests welcome
        // Vessel description storage and related code.
        [KSPField(isPersistant = true)]
        public string vesselDescription = string.Empty;
        private string vesselDescriptionForDisplay = string.Empty;
        private readonly string editorNewline = ((char)0x0a).ToString();
        private string lastVesselDescription = string.Empty;

        internal List<string> storedStringsArray = new List<string>();

        // Processing cache!
        //private readonly List<IJSIModule> installedModules = new List<IJSIModule>();
        private readonly DefaultableDictionary<string, object> resultCache = new DefaultableDictionary<string, object>(null);
        private readonly DefaultableDictionary<string, RPMVesselComputer.VariableCache> variableCache = new DefaultableDictionary<string, RPMVesselComputer.VariableCache>(null);
        private uint masterSerialNumber = 0u;

        // Diagnostics
        private int debug_fixedUpdates = 0;
        private DefaultableDictionary<string, int> debug_callCount = new DefaultableDictionary<string, int>(0);

        [KSPField(isPersistant = true)]
        public string RPMCid = string.Empty;
        private Guid id = Guid.Empty;

        private ExternalVariableHandlers plugins = null;
        internal Dictionary<string, Color32> overrideColors = new Dictionary<string, Color32>();

        // Public functions:
        // Request the instance, create it if one doesn't exist:
        public static RasterPropMonitorComputer Instantiate(MonoBehaviour referenceLocation, bool createIfMissing)
        {
            var thatProp = referenceLocation as InternalProp;
            var thatPart = referenceLocation as Part;
            if (thatPart == null)
            {
                if (thatProp == null)
                {
                    //throw new ArgumentException("Cannot instantiate RPMC in this location.");
                    return null;
                }
                thatPart = thatProp.part;
            }
            for (int i = 0; i < thatPart.Modules.Count; i++)
            {
                if (thatPart.Modules[i].ClassName == typeof(RasterPropMonitorComputer).Name)
                {
                    return thatPart.Modules[i] as RasterPropMonitorComputer;
                }
            }
            return (createIfMissing) ? thatPart.AddModule(typeof(RasterPropMonitorComputer).Name) as RasterPropMonitorComputer : null;
        }

        /// <summary>
        /// Wrapper for ExternalVariablesHandler.ProcessVariable.
        /// </summary>
        /// <param name="variable"></param>
        /// <param name="result"></param>
        /// <param name="cacheable"></param>
        /// <returns></returns>
        internal bool ProcessVariable(string variable, out object result, out bool cacheable)
        {
            return plugins.ProcessVariable(variable, out result, out cacheable);
        }

        // Page handler interface for vessel description page.
        // Analysis disable UnusedParameter
        public string VesselDescriptionRaw(int screenWidth, int screenHeight)
        {
            // Analysis restore UnusedParameter
            return vesselDescriptionForDisplay.UnMangleConfigText();
        }

        // Analysis disable UnusedParameter
        public string VesselDescriptionWordwrapped(int screenWidth, int screenHeight)
        {
            // Analysis restore UnusedParameter
            return JUtil.WordWrap(vesselDescriptionForDisplay.UnMangleConfigText(), screenWidth);
        }

        /// <summary>
        /// This intermediary will cache the results so that multiple variable
        /// requests within the frame would not result in duplicated code.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public object ProcessVariable(string input)
        {
            if (RPMGlobals.debugShowVariableCallCount)
            {
                debug_callCount[input] = debug_callCount[input] + 1;
            }

            RPMVesselComputer.VariableCache vc = variableCache[input];
            if (vc != null)
            {
                if (!(vc.cacheable && vc.serialNumber == masterSerialNumber))
                {
                    try
                    {
                        object newValue = vc.accessor(input, this);
                        vc.serialNumber = masterSerialNumber;
                        vc.cachedValue = newValue;
                    }
                    catch (Exception e)
                    {
                        JUtil.LogErrorMessage(this, "Processing error while processing {0}: {1}", input, e.Message);
                    }
                }

                return vc.cachedValue;
            }
            else
            {
                bool cacheable;
                RPMVesselComputer.VariableEvaluator evaluator = GetEvaluator(input, RPMVesselComputer.Instance(vessel), out cacheable);
                if (evaluator != null)
                {
                    vc = new RPMVesselComputer.VariableCache(cacheable, evaluator);
                    try
                    {
                        object newValue = vc.accessor(input, this);
                        vc.serialNumber = masterSerialNumber;
                        vc.cachedValue = newValue;
                    }
                    catch (Exception e)
                    {
                        JUtil.LogErrorMessage(this, "Processing error while processing {0}: {1}", input, e.Message);
                    }

                    variableCache[input] = vc;
                    return vc.cachedValue;
                }
            }

            RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
            return comp.ProcessVariableEx(input, this);
        }

        #region Monobehaviour
        public void Start()
        {
            if (!HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onVesselWasModified.Add(onVesselWasModified);

                if (string.IsNullOrEmpty(RPMCid))
                {
                    id = Guid.NewGuid();
                    RPMCid = id.ToString();
                    JUtil.LogMessage(this, "Start: Creating GUID {0}", id);
                }
                else
                {
                    id = new Guid(RPMCid);
                    JUtil.LogMessage(this, "Start: Loading GUID string {0} into {1}", RPMCid, id);
                }

                plugins = new ExternalVariableHandlers(part);

                RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
                if (!string.IsNullOrEmpty(vesselDescription))
                {
                    comp.SetVesselDescription(vesselDescription);
                }

                // Make sure we have the description strings parsed.
                string[] descriptionStrings = vesselDescription.UnMangleConfigText().Split(JUtil.LineSeparator, StringSplitOptions.None);
                for (int i = 0; i < descriptionStrings.Length; i++)
                {
                    if (descriptionStrings[i].StartsWith("AG", StringComparison.Ordinal) && descriptionStrings[i][3] == '=')
                    {
                        uint groupID;
                        if (uint.TryParse(descriptionStrings[i][2].ToString(), out groupID))
                        {
                            descriptionStrings[i] = string.Empty;
                        }
                    }
                }
                vesselDescriptionForDisplay = string.Join(Environment.NewLine, descriptionStrings).MangleConfigText();
                if (string.IsNullOrEmpty(vesselDescriptionForDisplay))
                {
                    vesselDescriptionForDisplay = " "; // Workaround for issue #466.
                }

                // Now let's parse our stored strings...
                if (!string.IsNullOrEmpty(storedStrings))
                {
                    var storedStringsSplit = storedStrings.Split('|');
                    for (int i = 0; i < storedStringsSplit.Length; ++i)
                    {
                        storedStringsArray.Add(storedStringsSplit[i]);
                    }
                }

                // TODO: If there are triggered events, register for an undock
                // callback so we can void and rebuild the callbacks after undocking.
                // Although it didn't work when I tried it...
                if (!string.IsNullOrEmpty(triggeredEvents))
                {
                    string[] varstring = triggeredEvents.Split('|');
                    for (int i = 0; i < varstring.Length; ++i)
                    {
                        comp.AddTriggeredEvent(varstring[i].Trim());
                    }
                }

                ConfigNode[] moduleConfigs = part.partInfo.partConfig.GetNodes("MODULE");
                for (int moduleId = 0; moduleId < moduleConfigs.Length; ++moduleId)
                {
                    if (moduleConfigs[moduleId].GetValue("name") == moduleName)
                    {
                        ConfigNode[] overrideColorSetup = moduleConfigs[moduleId].GetNodes("RPM_COLOROVERRIDE");
                        for(int colorGrp=0; colorGrp < overrideColorSetup.Length; ++colorGrp)
                        {
                            ConfigNode[] colorConfig = overrideColorSetup[colorGrp].GetNodes("COLORDEFINITION");
                            for (int defIdx = 0; defIdx < colorConfig.Length; ++defIdx)
                            {
                                if (colorConfig[defIdx].HasValue("name") && colorConfig[defIdx].HasValue("color"))
                                {
                                    string name = "COLOR_" + (colorConfig[defIdx].GetValue("name").Trim());
                                    Color32 color = ConfigNode.ParseColor32(colorConfig[defIdx].GetValue("color").Trim());
                                    if (overrideColors.ContainsKey(name))
                                    {
                                        overrideColors[name] = color;
                                    }
                                    else
                                    {
                                        overrideColors.Add(name, color);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void FixedUpdate()
        {
            ++masterSerialNumber;
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                // well, it looks sometimes it might become null..
                string s = EditorLogic.fetch.shipDescriptionField != null ? EditorLogic.fetch.shipDescriptionField.text : string.Empty;
                if (s != lastVesselDescription)
                {
                    lastVesselDescription = s;
                    // For some unclear reason, the newline in this case is always 0A, rather than Environment.NewLine.
                    vesselDescription = s.Replace(editorNewline, "$$$");
                }
            }
        }

        public void OnDestroy()
        {
            GameEvents.onVesselWasModified.Remove(onVesselWasModified);

            if (RPMGlobals.debugShowVariableCallCount)
            {
                List<KeyValuePair<string, int>> l = new List<KeyValuePair<string, int>>();
                l.AddRange(debug_callCount);
                l.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
                {
                    return a.Value - b.Value;
                });
                for (int i = 0; i < l.Count; ++i)
                {
                    JUtil.LogMessage(this, "{0} queried {1} times {2:0.0} calls/FixedUpdate", l[i].Key, l[i].Value, (float)(l[i].Value) / (float)(debug_fixedUpdates));
                }
            }

            variableCache.Clear();
        }

        private void onVesselWasModified(Vessel who)
        {
            if (who.id == vessel.id)
            {
                JUtil.LogMessage(this, "onVesselWasModified(): for me {0}", who.id);
                variableCache.Clear();
            }
        }
        #endregion
    }
}
