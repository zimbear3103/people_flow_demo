using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    [System.Serializable]
    public class LevelPrefabs
    {
        public GameObject hole;

        public GameObject factorie;

        public GameObject lane;

        public GameObject character;

        public GameObject road;

        public bool IsComplete => hole != null && lane != null && character != null;

        public GameObject HoleFor(int index) => hole;

        public GameObject LaneFor(int index) => lane;

        public bool HasFactory => factorie != null;

        public GameObject FactoryFor(int index) => factorie;

        public string MissingList()
        {
            var missing = new List<string>(3);
            if (hole == null) missing.Add("Hole");
            if (lane == null) missing.Add("Lane");
            if (character == null) missing.Add("Character");
            return string.Join(", ", missing);
        }

        public void OverrideWith(LevelPrefabs other)
        {
            if (other == null) return;
            if (other.hole != null) hole = other.hole;
            if (other.factorie != null) factorie = other.factorie;
            if (other.lane != null) lane = other.lane;
            if (other.character != null) character = other.character;
            if (other.road != null) road = other.road;
        }
    }
}
