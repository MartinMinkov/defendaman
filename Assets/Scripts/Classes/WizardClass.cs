﻿/*---------------------------------------------------------------------------------------
--  SOURCE FILE:    WizardClass.cs
--
--  PROGRAM:        Linux Game
--
--  FUNCTIONS:
--      override float basicAttack(Vector2 dir)
--      override float specialAttack(Vector2 dir)
--
--  DATE:           March 9, 2016
--
--  REVISIONS:      (Date and Description)
--
--  DESIGNERS:      Hank Lo
--
--  PROGRAMMER:     Hank Lo, Allen Tsang
--
--  NOTES:
--  This class contains the logic that relates to the Wizard Class.
---------------------------------------------------------------------------------------*/
using UnityEngine;
using System.Collections;

public class WizardClass : RangedClass
{
    int[] distance = new int[2]{ 20, 0 };
    int[] speed = new int[2] { 60, 0 };
    Rigidbody2D fireball;
    Rigidbody2D magicCircle;

    new void Start()
    {
        base.Start();
        fireball = (Rigidbody2D)Resources.Load("Prefabs/Fireball", typeof(Rigidbody2D));
        magicCircle = (Rigidbody2D)Resources.Load("Prefabs/MagicCircle", typeof(Rigidbody2D));

        var controller = Resources.Load("Controllers/magegirl") as RuntimeAnimatorController;
        gameObject.GetComponent<Animator>().runtimeAnimatorController = controller;

        au_simple_attack = Resources.Load("Music/Weapons/magegirl_staff_primary") as AudioClip;
        au_special_attack = Resources.Load("Music/Weapons/magegirl_staff_secondary") as AudioClip;

    }



    public WizardClass()
	{
        this._className = "Wizard";
        this._classDescription = "Wingardium Leviosa. No, not leviosAA, leviOsa.";
        this._classStat.MaxHp = 100;
        this._classStat.CurrentHp = this._classStat.MaxHp;

        //placeholder numbers
        this._classStat.MoveSpeed = 8;
        this._classStat.AtkPower = 3;
        this._classStat.Defense  = 5;
        
        cooldowns = new float[2] { 0.5f, 6 };
	}

    /*---------------------------------------------------------------------------------------------------------------------
    -- FUNCTION: basicAttack
    --
    -- DATE: March 9, 2016
    --
    -- REVISIONS: None
    --
    -- DESIGNER: Hank Lo
    --
    -- PROGRAMMER: Hank Lo
    --
    -- INTERFACE: float basicAttack(Vector2 dir)
    --              dir: a vector2 object which shows the direction of the attack
    --
    -- RETURNS: a float representing the cooldown of the attack
    --
    -- NOTES:
    -- Function that's called when the wizard uses the left click attack
    ---------------------------------------------------------------------------------------------------------------------*/
    public override float basicAttack(Vector2 dir)
    {
        dir = ((Vector2)((Vector3)dir - transform.position)).normalized;
        base.basicAttack(dir);

        Rigidbody2D attack = (Rigidbody2D)Instantiate(fireball, transform.position, transform.rotation);
        attack.AddForce(dir * speed[0]);
        attack.GetComponent<Fireball>().playerID = playerID;
        attack.GetComponent<Fireball>().teamID = team;
        attack.GetComponent<Fireball>().damage = ClassStat.AtkPower;
        attack.GetComponent<Fireball>().maxDistance = distance[0];

        return cooldowns[0];
    }

    /*---------------------------------------------------------------------------------------------------------------------
    -- FUNCTION: specialAttack
    --
    -- DATE: March 9, 2016
    --
    -- REVISIONS:
    --      - March 17, 2016: Fixed instantiation to work through networking
    --
    -- DESIGNER: Hank Lo
    --
    -- PROGRAMMER: Hank Lo
    --
    -- INTERFACE: float specialAttack(Vector2 dir)
    --              dir: a vector2 object which shows the direction of the attack
    --
    -- RETURNS: a float representing the cooldown of the attack
    --
    -- NOTES:
    -- Function that's called when the wizard uses the right click special attack (magic circle)
    ---------------------------------------------------------------------------------------------------------------------*/
    public override float specialAttack(Vector2 dir)
    {
        base.specialAttack(dir);

        //Vector2 mousePos = Input.mousePosition;
        //mousePos = Camera.main.ScreenToWorldPoint(mousePos);
        //var distance = (dir - (Vector2) transform.position).magnitude;
        //Vector2 endp = (Vector2) transform.position + (distance * dir);

        Rigidbody2D attack = (Rigidbody2D)Instantiate(magicCircle, dir, Quaternion.identity);
        attack.GetComponent<MagicCircle>().playerID = playerID;
        attack.GetComponent<MagicCircle>().teamID = team;
        attack.GetComponent<MagicCircle>().damage = ClassStat.AtkPower * 0;

        return cooldowns[1];
    }
}
