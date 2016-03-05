﻿using UnityEngine;
using System.Collections;

public class NinjaClass : BaseClass
{

	public NinjaClass()
	{
        this._className = "Ninja";
        this._classDescription = "Test class Ninja";
        this._classStat.CurrentHp = 50;
        this._classStat.MaxHp = 150;

        //placeholder numbers
        this._classStat.MoveSpeed = 25;
        this._classStat.AtkPower = 15;

        var controller = Resources.Load("Controllers/ninjaboi") as RuntimeAnimatorController;
        gameObject.GetComponent<Animator>().runtimeAnimatorController = controller;

        cooldowns = new float[2] { 0.5f, 2 };
    }

    //attacks return time it takes to execute
    public override float basicAttack(Vector2 dir)
    {
        base.basicAttack(dir);
        return 0;
    }

    public override float specialAttack(Vector2 dir)
    {
        base.specialAttack(dir);
        return 2;
    }
}