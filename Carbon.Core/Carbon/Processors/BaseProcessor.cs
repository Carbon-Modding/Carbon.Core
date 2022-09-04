﻿namespace Carbon.Core.Processors
{
    public class BaseProcessor : FacepunchBehaviour
    {
        public bool IsInitialized { get; set; }
    
        public virtual void Start ()
        {
            DontDestroyOnLoad ( gameObject );

            IsInitialized = true;
        }
    }
}
