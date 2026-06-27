using System;

namespace WordFlow.Adventure.Net
{
    /// <summary>Subset of the backend GradeResponse we read for the debug HUD. JsonUtility-friendly.</summary>
    [Serializable]
    public sealed class GradeResponse
    {
        public float par;
        public string grade;
    }
}
