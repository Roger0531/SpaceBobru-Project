using System;
using UnityEngine;

[Serializable]
public class EventChoice
{
    public int choiceID;
    public bool isFinal = false;
    public int hoursTakes;
    public string ChoiceText;
    public string ChoiceDescription;
    public double energyCost;
    public double oxygenCost;
    public double foodCost;
    public double hpCost;
}
