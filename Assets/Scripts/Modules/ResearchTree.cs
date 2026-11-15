using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine;

public enum ResearchEffect { None, SolarOutput, OxygenOutput, RepairSpeed, StorageCapacity }

[Serializable]
public class ResearchNode
{
    public string id;
    public string displayName;
    [TextArea] public string description;
    public float cost = 50f;
    public string[] requires;
    public ResearchEffect effect;
    public float effectValue = 0.1f;   // наприклад 0.1 = +10%
}

public class ResearchTree: MonoBehaviour
{
    public static ResearchTree Instance { get; private set; }

    [SerializeField] List<ResearchNode> nodes = new();

    readonly HashSet<string> unlocked = new();

    public IReadOnlyList<ResearchNode> Nodes => nodes;
    public event Action<ResearchNode> OnUnlocked;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public ResearchNode Get(string id) => nodes.Find(n => n.id == id);
    public bool IsUnlocked(string id) => unlocked.Contains(id);

    public bool CanUnlock(ResearchNode node)
    {
        if (node == null || IsUnlocked(node.id)) return false;
        if (Research.Points < node.cost) return false;

        if (node.requires != null)
            foreach (var req in node.requires)
                if (!IsUnlocked(req)) return false;

        return true;
    }

    public bool TryUnlock(string id)
    {
        var node = Get(id);
        if (!CanUnlock(node)) return false;
        if (!Research.TrySpend(node.cost)) return false;

        unlocked.Add(node.id);
        OnUnlocked?.Invoke(node);
        return true;
    }

    /// <summary>Сумарний бонус від усіх відкритих вузлів з цим ефектом.</summary>
    public float GetBonus(ResearchEffect effect)
    {
        float sum = 0f;
        foreach (var n in nodes)
            if (n.effect == effect && unlocked.Contains(n.id)) sum += n.effectValue;
        return sum;
    }
}
