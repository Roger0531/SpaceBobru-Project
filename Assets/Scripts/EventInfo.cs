using NUnit.Framework;
using System;
using UnityEngine;
using System.Collections.Generic;

[Serializable]
public class EventInfo
{
    public int eventID;
    public string eventName;
    public string eventDescription;
    public int dayOccurs;
    public int hourOccurs;
    public int hoursTakes;
    //public int[] rolesThatCanDo;
    public double energyCost;
    public double oxygenCost;
    public double foodCost;
    public double hpCost;
    public int subEventID;
    public List<EventChoice> choices;
    public bool isCompleted = false; 
}
