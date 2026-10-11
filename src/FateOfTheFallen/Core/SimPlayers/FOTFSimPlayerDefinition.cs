using System;

namespace FateOfTheFallen
{
    internal sealed class FOTFSimPlayerDefinition
    {
        internal string Id;
        internal string ClassName;
        internal string[] NameCandidates;
        internal string Gender;
        internal int StartingLevel;
        internal string HairIndex;
        internal int HairColorIndex;
        internal int SkinColorIndex;
        internal int Personality;
        internal int BioIndex;
        internal int HideHelm;

        internal string PreferredName
        {
            get
            {
                if (NameCandidates == null ||
                    NameCandidates.Length == 0)
                {
                    return string.Empty;
                }

                return NameCandidates[0] ?? string.Empty;
            }
        }
    }
}
