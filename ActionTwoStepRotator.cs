/* This content is licensed under the terms of the Creative Commons Attribution 4.0 International License.
 * When using this content, you must:
 * �    Acknowledge that the content is from the Sansar Knowledge Base.
 * �    Include our copyright notice: "� 2018 Linden Research, Inc."
 * �    Indicate that the content is licensed under the Creative Commons Attribution-Share Alike 4.0 International License.
 * �    Include the URL for, or link to, the license summary at https://creativecommons.org/licenses/by-sa/4.0/deed.hi (and, if possible, to the complete license terms at https://creativecommons.org/licenses/by-sa/4.0/legalcode.
 * For example:
 * "This work uses content from the Sansar Knowledge Base. � 2018 Linden Research, Inc. Licensed under the Creative Commons Attribution 4.0 International License (license summary available at https://creativecommons.org/licenses/by/4.0/ and complete license terms available at https://creativecommons.org/licenses/by/4.0/legalcode)."
 */
using Sansar;
using Sansar.Script;
using Sansar.Simulation;
using System;
using System.Collections.Generic;
using System.Linq;

public class ActionTwoStepRotator : SceneObjectScript
{

    //Script uses ObjectPrivate.Mover .... Moves from current position to a new relative position.  Triggered via simple messages.

    #region ConstantsVariables
    // Public properties

    //[DefaultValue(".")]
    //public Interaction MyInteraction;

    // Offset from base position, in objects local space
    //[DisplayName("Position Offset")]
    //public readonly Vector PositionOffset;

    [DisplayName("Rotation Offset")]
    public readonly Vector RotationOffset;

    // On hearing this event execute move
    [DisplayName("On Event")]
    public readonly string OnEvent;

    // On hearing this event execute return move
    [DisplayName("Off Event")]
    public readonly string OffEvent;

    [DefaultValue(2.0f)]
    public double Seconds;

    public MoveMode moveMode = MoveMode.Smoothstep;

    public bool AutoOff = false;
    
    public bool UpdateFromMovePosition = false;

    public bool StartMoved = false;

    private Quaternion initialRotation;
    private Quaternion fixedRotation;
    private Vector NewRot;

    #endregion

    // Logic!

    private void OnEventExecute(ScriptEventData data)
    {
        //Log.Write("Heard: " + data.Message);
        if (ObjectPrivate.IsMovable)
        {
            if (UpdateFromMovePosition)
            {
                //Log.Write("ObjectPrivate.Rotation: " + ObjectPrivate.Rotation);
                //Log.Write("RotationOffset: " + RotationOffset);
                Quaternion movedRotation = Quaternion.FromEulerAngles(NewRot) * ObjectPrivate.Rotation;
                //Quaternion NewRotate = ObjectPrivate.Rotation * RotationOffset;
                //Log.Write("ON from MovePosition");
                ObjectPrivate.Mover.AddRotate(movedRotation, Seconds, moveMode);
            }
            else
            {
                //Log.Write("initalRotation: " + initialRotation);
                //Log.Write("RotationOffset: " + RotationOffset);
                //Quaternion NewRotate = initialRotation * RotationOffset;
                //Log.Write("NewRotate: " + NewRotate);
                //Quaternion rotation = Quaternion.FromAngleAxis(90f, Vector.Up);

                //Log.Write("Initial Rotation: " + initialRotation);
                //Log.Write("Rotation Offset: " + RotationOffset);
                //Log.Write("rotation: " + rotation);
                //Log.Write("On no MovePosition");
                if (AutoOff)
                {
                    WaitFor(ObjectPrivate.Mover.AddRotate, fixedRotation, Seconds, moveMode);
                    OffEventExecute(data);
                }
                else
                {
                    ObjectPrivate.Mover.AddRotate(fixedRotation, Seconds, moveMode);
                }

            }
        }
    }

    private void OffEventExecute(ScriptEventData data)
    {
        //Log.Write("Off");
        if (ObjectPrivate.IsMovable)
        {
            if (UpdateFromMovePosition)
            {
                Vector BackRotationOffset = NewRot * -1;
                Quaternion rotation = Quaternion.FromEulerAngles(BackRotationOffset) * ObjectPrivate.Rotation;
                //Log.Write("Back from MovePosition");
                ObjectPrivate.Mover.AddRotate(rotation, Seconds, moveMode);
            }
            else
            {
                //Log.Write("Back no MovePosition");
                ObjectPrivate.Mover.AddRotate(initialRotation, Seconds, moveMode);
            }
        }
    }

    #region SimpleHelpers v2
    // Update the region tag above by incrementing the version when updating anything in the region.

    // If a Group is set, will only respond and send to other SimpleScripts with the same Group tag set.
    // Does NOT accept CSV lists of groups.
    // To send or receive events to/from a specific group from outside that group prepend the group name with a > to the event name
    // my_group>on
    [DefaultValue("")]
    [DisplayName("Group")]
    public string Group = "";

    public interface ISimpleData
    {
        AgentInfo AgentInfo { get; }
        ObjectId ObjectId { get; }
        ObjectId SourceObjectId { get; }

        // Extra data
        Reflective ExtraData { get; }
    }

    public class SimpleData : Reflective, ISimpleData
    {
        public SimpleData(ScriptBase script) { ExtraData = script; }
        public AgentInfo AgentInfo { get; set; }
        public ObjectId ObjectId { get; set; }
        public ObjectId SourceObjectId { get; set; }

        public Reflective ExtraData { get; }
    }

    public interface IDebugger { bool DebugSimple { get; } }
    private bool __debugInitialized = false;
    private bool __SimpleDebugging = false;
    private string __SimpleTag = "";

    private string GenerateEventName(string eventName)
    {
        eventName = eventName.Trim();
        if (eventName.EndsWith("@"))
        {
            // Special case on@ to send the event globally (the null group) by sending w/o the @.
            return eventName.Substring(0, eventName.Length - 1);
        }
        else if (Group == "" || eventName.Contains("@"))
        {
            // No group was set or already targeting a specific group as is.
            return eventName;
        }
        else
        {
            // Append the group
            return $"{eventName}@{Group}";
        }
    }

    private void SetupSimple()
    {
        __debugInitialized = true;
        __SimpleTag = GetType().Name + " [S:" + Script.ID.ToString() + " O:" + ObjectPrivate.ObjectId.ToString() + "]";
        Wait(TimeSpan.FromSeconds(1));
        IDebugger debugger = ScenePrivate.FindReflective<IDebugger>("SimpleDebugger").FirstOrDefault();
        if (debugger != null) __SimpleDebugging = debugger.DebugSimple;
    }

    System.Collections.Generic.Dictionary<string, Func<string, Action<ScriptEventData>, Action>> __subscribeActions = new System.Collections.Generic.Dictionary<string, Func<string, Action<ScriptEventData>, Action>>();
    private Action SubscribeToAll(string csv, Action<ScriptEventData> callback)
    {
        if (!__debugInitialized) SetupSimple();
        if (string.IsNullOrWhiteSpace(csv)) return null;

        Func<string, Action<ScriptEventData>, Action> subscribeAction;
        if (__subscribeActions.TryGetValue(csv, out subscribeAction))
        {
            return subscribeAction(csv, callback);
        }

        // Simple case.
        if (!csv.Contains(">>"))
        {
            __subscribeActions[csv] = SubscribeToAllInternal;
            return SubscribeToAllInternal(csv, callback);
        }

        // Chaining
        __subscribeActions[csv] = (_csv, _callback) =>
        {
            System.Collections.Generic.List<string> chainedCommands = new System.Collections.Generic.List<string>(csv.Split(new string[] { ">>" }, StringSplitOptions.RemoveEmptyEntries));

            string initial = chainedCommands[0];
            chainedCommands.RemoveAt(0);
            chainedCommands.Add(initial);

            Action unsub = null;
            Action<ScriptEventData> wrappedCallback = null;
            wrappedCallback = (data) =>
            {
                string first = chainedCommands[0];
                chainedCommands.RemoveAt(0);
                chainedCommands.Add(first);
                if (unsub != null) unsub();
                unsub = SubscribeToAllInternal(first, wrappedCallback);
                Log.Write(LogLevel.Info, "CHAIN Subscribing to " + first);
                _callback(data);
            };

            unsub = SubscribeToAllInternal(initial, wrappedCallback);
            return unsub;
        };

        return __subscribeActions[csv](csv, callback);
    }

    private Action SubscribeToAllInternal(string csv, Action<ScriptEventData> callback)
    {
        Action unsubscribes = null;
        string[] events = csv.Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (__SimpleDebugging)
        {
            Log.Write(LogLevel.Info, __SimpleTag, "Subscribing to " + events.Length + " events: " + string.Join(", ", events));
        }
        Action<ScriptEventData> wrappedCallback = callback;

        foreach (string eventName in events)
        {
            if (__SimpleDebugging)
            {
                var sub = SubscribeToScriptEvent(GenerateEventName(eventName), (ScriptEventData data) =>
                {
                    Log.Write(LogLevel.Info, __SimpleTag, "Received event " + GenerateEventName(eventName));
                    wrappedCallback(data);
                });
                unsubscribes += sub.Unsubscribe;
            }
            else
            {
                var sub = SubscribeToScriptEvent(GenerateEventName(eventName), wrappedCallback);
                unsubscribes += sub.Unsubscribe;
            }
        }
        return unsubscribes;
    }

    System.Collections.Generic.Dictionary<string, Action<string, Reflective>> __sendActions = new System.Collections.Generic.Dictionary<string, Action<string, Reflective>>();
    private void SendToAll(string csv, Reflective data)
    {
        if (!__debugInitialized) SetupSimple();
        if (string.IsNullOrWhiteSpace(csv)) return;

        Action<string, Reflective> sendAction;
        if (__sendActions.TryGetValue(csv, out sendAction))
        {
            sendAction(csv, data);
            return;
        }

        // Simple case.
        if (!csv.Contains(">>"))
        {
            __sendActions[csv] = SendToAllInternal;
            SendToAllInternal(csv, data);
            return;
        }

        // Chaining
        System.Collections.Generic.List<string> chainedCommands = new System.Collections.Generic.List<string>(csv.Split(new string[] { ">>" }, StringSplitOptions.RemoveEmptyEntries));
        __sendActions[csv] = (_csv, _data) =>
        {
            string first = chainedCommands[0];
            chainedCommands.RemoveAt(0);
            chainedCommands.Add(first);

            Log.Write(LogLevel.Info, "CHAIN Sending to " + first);
            SendToAllInternal(first, _data);
        };
        __sendActions[csv](csv, data);
    }

    private void SendToAllInternal(string csv, Reflective data)
    {
        if (string.IsNullOrWhiteSpace(csv)) return;
        string[] events = csv.Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        if (__SimpleDebugging) Log.Write(LogLevel.Info, __SimpleTag, "Sending " + events.Length + " events: " + string.Join(", ", events) + (Group != "" ? (" to group " + Group) : ""));
        foreach (string eventName in events)
        {
            PostScriptEvent(GenerateEventName(eventName), data);
        }
    }
    #endregion

    public override void Init()
    {
        initialRotation = ObjectPrivate.Rotation;
        NewRot = RotationOffset;
        NewRot.X = NewRot.X * 0.0174533f;
        NewRot.Y = NewRot.Y * 0.0174533f;
        NewRot.Z = NewRot.Z * 0.0174533f;
        fixedRotation = Quaternion.FromEulerAngles(NewRot) * initialRotation;
        if (StartMoved) ObjectPrivate.Mover.AddRotate(fixedRotation, Seconds, moveMode);

        if (OnEvent.Length > 0)
        {
            List<string> OnArray = new List<string>();
            OnArray.Clear();
            OnEvent.Replace(" ", string.Empty);
            OnArray = OnEvent.Split(',').ToList();
            int i = 0;
            do
            {
                SubscribeToAll(OnArray[i], OnEventExecute);
                //Log.Write("Subscribing to " + OnArray[i]);
                i++;
            } while (i < OnArray.Count());
        }

        if (OffEvent.Length> 0)
        {
            List<string> OffArray = new List<string>();
            OffArray.Clear();
            OffEvent.Replace(" ", string.Empty);
            OffArray = OffEvent.Split(',').ToList();
            int i = 0;
            do
            {
                SubscribeToAll(OffArray[i], OffEventExecute);
                //Log.Write("Subscribing to " + OffArray[i]);
                i++;
            } while (i < OffArray.Count());
        }

        //Log.Write("Past Init");
    }

}
