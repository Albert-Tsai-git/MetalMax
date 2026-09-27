using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Save
{
    /// <summary>
    /// 存档数据（JSON）。只保存数据 ID 与可变状态，读档时通过 GameDB 还原引用。
    /// 结构变化时提升 CurrentVersion，并在 SaveSerializer.Migrate 中补迁移。
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 4;

        public int version = CurrentVersion;
        public string savedAt;
        public int gold;
        public int exp;
        public string scene;
        public Vector3 position;
        public List<CombatantSave> party = new();
        public List<PartSave> inventory = new();
        // v2
        public string lastTown;
        public List<Game.Town.BountyRecord> bounties = new();
        // v4
        public List<string> flags = new();
        public List<Game.Story.QuestRecord> quests = new();
    }

    [Serializable]
    public class CombatantSave
    {
        public string id;
        public int level, exp;  // v3
        public int maxHp, hp, attack, defense, speed, evade;
        public bool inTank;
        /// <summary>没有战车时为 null（JsonUtility 会写成空对象，用 hasTank 区分）</summary>
        public bool hasTank;
        public TankSave tank;
    }

    [Serializable]
    public class TankSave
    {
        public string tankName;
        public PartSave chassis, engine, cUnit;
        /// <summary>与武器孔一一对应，空孔的 id 为空串</summary>
        public List<PartSave> weapons = new();
        public float armorTons;
        public int currentSp;
    }

    [Serializable]
    public class PartSave
    {
        public string id;
        public int upgradeLevel;
        public int condition;
        public int ammo;
    }
}
