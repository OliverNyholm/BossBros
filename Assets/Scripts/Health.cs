﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Health : NetworkBehaviour
{
    [SerializeField]
    private GameObject myFloatingHealthPrefab;

    [SyncVar]
    public int myMaxHealth = 100;
    [SyncVar]
    public int myCurrentHealth = 100;

    public delegate void HealthChanged(float aHealthPercentage, string aHealthText, int aShieldValue);
    public delegate void HealthChangedParty(float aHealthPercentage, NetworkInstanceId anID);
    public delegate void ThreadGenerated(int aThreatPercentage, NetworkInstanceId anID);
    public delegate void HealthZero();

    [SyncEvent]
    public event HealthChanged EventOnHealthChange;
    [SyncEvent]
    public event HealthChangedParty EventOnHealthChangeParty;
    [SyncEvent]
    public event ThreadGenerated EventOnThreatGenerated;
    [SyncEvent]
    public event HealthZero EventOnHealthZero;

    private List<BuffShieldSpell> myShields = new List<BuffShieldSpell>();

    public void TakeDamage(int aValue)
    {
        if (!isServer)
        {
            return;
        }

        int damage = CalculateMitigations(aValue);
        myCurrentHealth -= damage;
        if (myCurrentHealth <= 0)
        {
            myCurrentHealth = 0;
            EventOnHealthZero();
        }

        string damageText = damage.ToString();
        if(aValue != damage)
        {
            int absorbed = aValue - damage;
            damageText = damage.ToString() + " (" + absorbed.ToString() + " absorbed)";
        }

        OnHealthChanged();
        RpcSpawnFloatingText(damageText, Color.red);
    }

    public void GainHealth(int aValue)
    {
        if (!isServer)
        {
            return;
        }

        myCurrentHealth += aValue;
        if (myCurrentHealth > myMaxHealth)
        {
            myCurrentHealth = myMaxHealth;
        }

        OnHealthChanged();
        RpcSpawnFloatingText(aValue.ToString(), Color.yellow);
    }

    public bool IsDead()
    {
        return myCurrentHealth <= 0;
    }

    public float GetHealthPercentage()
    {
        return (float)myCurrentHealth / myMaxHealth;
    }

    public int MaxHealth
    {
        get { return myMaxHealth; }
        set
        {
            myMaxHealth = value;
            OnHealthChanged();
        }
    }

    public void AddShield(BuffShieldSpell aShield)
    {
        if (!isServer)
            return;

        myShields.Add(aShield);
        OnHealthChanged();
        RpcSpawnFloatingText("Shield, " + aShield.GetRemainingShieldHealth().ToString(), Color.yellow);
    }

    public void RemoveShield()
    {
        if (!isServer)
            return;

        for (int index = 0; index < myShields.Count; index++)
        {
            if (myShields[index].IsFinished())
            {
                myShields.RemoveAt(index);
                break;
            }
        }

        OnHealthChanged();
        RpcSpawnFloatingText("Shield faded", Color.yellow);
    }

    public void GenerateThreat(int aThreatValue, NetworkInstanceId anID)
    {
        EventOnThreatGenerated?.Invoke(aThreatValue, anID);
    }

    private void OnHealthChanged()
    {
        EventOnHealthChangeParty?.Invoke(GetHealthPercentage(), GetComponent<NetworkIdentity>().netId);
        EventOnHealthChange?.Invoke(GetHealthPercentage(), myCurrentHealth.ToString() + "/" + MaxHealth, GetTotalShieldValue());
    }

    private void OnHealthZero()
    {
        EventOnHealthZero?.Invoke();
    }

    [ClientRpc]
    private void RpcSpawnFloatingText(string aText, Color aColor)
    {
        GameObject floatingHealthGO = Instantiate(myFloatingHealthPrefab, transform);
        FloatingHealth floatingHealth = floatingHealthGO.GetComponent<FloatingHealth>();
        floatingHealth.SetText(aText, aColor);
    }

    public int CalculateMitigations(int anIncomingDamageValue)
    {
        Stats parentStats = GetComponent<Stats>();

        int damage = (int)(anIncomingDamageValue * parentStats.myDamageMitigator);

        for (int index = 0; index < myShields.Count; index++)
        {
            damage = myShields[index].SoakDamage(damage);

            if (damage <= 0)
                break;
        }

        return damage;
    }

    public int GetTotalShieldValue()
    {
        int shieldValue = 0;

        for (int index = 0; index < myShields.Count; index++)
        {
            shieldValue += myShields[index].GetRemainingShieldHealth();
        }

        return shieldValue;
    }
}
