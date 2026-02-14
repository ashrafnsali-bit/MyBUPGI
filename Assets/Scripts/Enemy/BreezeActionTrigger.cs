using UnityEngine;
using System.Collections.Generic;

namespace Breeze.Core
{
    public class BreezeActionTrigger : MonoBehaviour
    {
        public MonoBehaviour System;
        public int TriggerType;
        public List<string> CustomActions;
        public UnityEngine.Events.UnityEvent OnActionsStarted;
        public UnityEngine.Events.UnityEvent OnActionChanged;
        public UnityEngine.Events.UnityEvent OnActionsEnd;
        public bool Done = true;

        void Start() { }
    }
}
