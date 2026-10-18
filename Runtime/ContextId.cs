using UnityEngine;

namespace TraceForge
{
    internal static class ContextId
    {
        internal static ulong Capture(Object context)
        {
            if (context == null)
                return 0;
#if UNITY_6000_6_OR_NEWER
            return EntityId.ToULong(context.GetEntityId());
#else
            return FromLegacyInstanceId(context.GetInstanceID());
#endif
        }

        internal static ulong FromLegacyInstanceId(int instanceId)
        {
            return unchecked((uint)instanceId);
        }

        internal static int ToLegacyInstanceId(ulong contextId)
        {
            return contextId <= uint.MaxValue ? unchecked((int)(uint)contextId) : 0;
        }
    }
}
