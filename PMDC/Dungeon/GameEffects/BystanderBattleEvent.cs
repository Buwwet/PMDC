﻿using System;
using System.Collections.Generic;
using RogueEssence.Data;
using RogueElements;
using RogueEssence.Content;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Dev;

namespace PMDC.Dungeon
{
    [Serializable]
    public class SupportAbilityEvent : BattleEvent
    {
        public int SupportAbility;

        public SupportAbilityEvent() { }
        public SupportAbilityEvent(int supportAbility)
        {
            SupportAbility = supportAbility;
        }
        protected SupportAbilityEvent(SupportAbilityEvent other)
        {
            SupportAbility = other.SupportAbility;
        }
        public override GameEvent Clone() { return new SupportAbilityEvent(this); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Category == BattleData.SkillCategory.Magical
                && context.User.HasIntrinsic(SupportAbility))
                context.AddContextStateMult<DmgMult>(false,4, 3);
            yield break;
        }
    }

    [Serializable]
    public class SnatchEvent : BattleEvent
    {
        public FiniteEmitter Emitter;
        [Sound(0)]
        public string Sound;

        public SnatchEvent() { Emitter = new EmptyFiniteEmitter(); }
        public SnatchEvent(FiniteEmitter emitter, string sound)
            : this()
        {
            Emitter = emitter;
            Sound = sound;
        }
        protected SnatchEvent(SnatchEvent other)
            : this()
        {
            Emitter = (FiniteEmitter)other.Emitter.Clone();
            Sound = other.Sound;
        }
        public override GameEvent Clone() { return new SnatchEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ContextStates.Contains<Redirected>())
                yield break;

            if (context.ActionType == BattleActionType.Trap || context.ActionType == BattleActionType.Item)
                yield break;

            //must be a status move
            if (context.Data.Category != BattleData.SkillCategory.Status)
                yield break;

            //attacker must be target
            if (context.User != context.Target)
                yield break;


            GameManager.Instance.BattleSE(Sound);
            FiniteEmitter endEmitter = (FiniteEmitter)Emitter.Clone();
            endEmitter.SetupEmit(ownerChar.MapLoc, ownerChar.MapLoc, ownerChar.CharDir);
            DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);

            CharAnimAction SpinAnim = new CharAnimAction(ownerChar.CharLoc, (context.Target.CharLoc - ownerChar.CharLoc).ApproximateDir8(), 05);//Attack
            SpinAnim.MajorAnim = true;

            yield return CoroutineManager.Instance.StartCoroutine(ownerChar.StartAnim(SpinAnim));
            yield return new WaitWhile(ownerChar.OccupiedwithAction);

            DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_SNATCH").ToLocal(), ownerChar.Name));
            context.Target = ownerChar;
            context.ContextStates.Set(new Redirected());
        }
    }


    //below, the effects deal exclusively with explosions

    [Serializable]
    public class AllyDifferentExplosionEvent : BattleEvent
    {
        //also need to somehow specify alternative animations/sounds
        public List<BattleEvent> BaseEvents;

        public AllyDifferentExplosionEvent() { BaseEvents = new List<BattleEvent>(); }
        protected AllyDifferentExplosionEvent(AllyDifferentExplosionEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new AllyDifferentExplosionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character targetChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(context.ExplosionTile);
            if (targetChar == null)
                yield break;

            if (DungeonScene.Instance.GetMatchup(context.User, targetChar) == Alignment.Friend)
            {
                //remove all MoveHit effects (except for the post-effect)
                context.Data.OnHits.Clear();
                context.Data.OnHitTiles.Clear();
                //remove BasePower component
                if (context.Data.SkillStates.Contains<BasePowerState>())
                    context.Data.SkillStates.Remove<BasePowerState>();

                //add the alternative effects
                foreach (BattleEvent battleEffect in BaseEvents)
                    context.Data.OnHits.Add(0, (BattleEvent)battleEffect.Clone());
            }
        }
    }

    [Serializable]
    public class DampEvent : BattleEvent
    {
        public int Div;
        StringKey Msg;

        public DampEvent() { }
        public DampEvent(int div, StringKey msg)
        {
            Div = div;
            Msg = msg;
        }
        protected DampEvent(DampEvent other)
        {
            Div = other.Div;
            Msg = other.Msg;
        }
        public override GameEvent Clone() { return new DampEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //only block explosions
            if (context.Explosion.Range == 0)
                yield break;

            //make sure to exempt Round.

            DungeonScene.Instance.LogMsg(String.Format(Msg.ToLocal(), ownerChar.Name));
            context.Explosion.Range = 0;
            context.Explosion.ExplodeFX = new BattleFX();
            context.Explosion.Emitter = new EmptyCircleSquareEmitter();
            context.Explosion.TileEmitter = new EmptyFiniteEmitter();
            if (Div > 0)
                context.AddContextStateMult<DmgMult>(false,1, Div);
            else
                context.AddContextStateMult<DmgMult>(false,Div, 1);
        }
    }

    [Serializable]
    public class DampItemEvent : BattleEvent
    {
        public override GameEvent Clone() { return new DampItemEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Throw)
            {
                ItemData entry = DataManager.Instance.GetItem(context.Item.ID);
                if (!entry.ItemStates.Contains<RecruitState>())
                {
                    context.Explosion.Range = 0;
                    context.Explosion.ExplodeFX = new BattleFX();
                    context.Explosion.Emitter = new EmptyCircleSquareEmitter();
                    context.Explosion.TileEmitter = new EmptyFiniteEmitter();
                }
            }
            yield break;
        }
    }


    [Serializable]
    public class CatchItemSplashEvent : BattleEvent
    {
        public override GameEvent Clone() { return new CatchItemSplashEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Throw)
            {
                //can't catch pierce
                if (context.HitboxAction is LinearAction && !((LinearAction)context.HitboxAction).StopAtHit)
                    yield break;

                Character targetChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(context.ExplosionTile);
                if (targetChar != null)
                {

                    //can't catch when holding
                    if (targetChar.EquippedItem.ID > -1)
                        yield break;

                    ItemData entry = DataManager.Instance.GetItem(context.Item.ID);

                    //can't catch recruit item under any circumstances
                    if (!entry.ItemStates.Contains<RecruitState>())
                    {

                        if (targetChar.MemberTeam is MonsterTeam)
                        {
                            //can't catch if it's a wild team, and it's an edible or ammo
                            if (entry.ItemStates.Contains<EdibleState>() || entry.ItemStates.Contains<AmmoState>())
                                yield break;
                        }

                        context.Explosion.Range = 0;
                        context.Explosion.ExplodeFX = new BattleFX();
                        context.Explosion.Emitter = new EmptyCircleSquareEmitter();
                        context.Explosion.TileEmitter = new EmptyFiniteEmitter();
                    }
                }
            }
        }
    }

    [Serializable]
    public class IsolateElementEvent : BattleEvent
    {
        [DataType(0, DataManager.DataType.Element, false)]
        public int Element;

        public IsolateElementEvent() { }
        public IsolateElementEvent(int element)
        {
            Element = element;
        }
        protected IsolateElementEvent(IsolateElementEvent other)
        {
            Element = other.Element;
        }
        public override GameEvent Clone() { return new IsolateElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (Element != 00 && context.Data.Element != Element)
                yield break;

            if (ZoneManager.Instance.CurrentMap.GetCharAtLoc(context.ExplosionTile) != ownerChar)
                yield break;

            context.Explosion.Range = 0;
        }
    }

    [Serializable]
    public class DrawAttackEvent : BattleEvent
    {
        [DataType(0, DataManager.DataType.Element, false)]
        public int Element;
        public Alignment DrawFrom;
        public StringKey Msg;

        public DrawAttackEvent() { }
        public DrawAttackEvent(Alignment drawFrom, int element, StringKey msg)
        {
            DrawFrom = drawFrom;
            Element = element;
            Msg = msg;
        }
        protected DrawAttackEvent(DrawAttackEvent other)
        {
            DrawFrom = other.DrawFrom;
            Element = other.Element;
            Msg = other.Msg;
        }
        public override GameEvent Clone() { return new DrawAttackEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ContextStates.Contains<Redirected>())
                yield break;

            if (context.ActionType == BattleActionType.Trap || context.ActionType == BattleActionType.Item)
                yield break;

            if (Element != 00 && context.Data.Element != Element)
                yield break;

            Character targetChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(context.ExplosionTile);
            if (targetChar == null)
                yield break;

            //the attack needs to be able to hit foes
            if ((context.HitboxAction.TargetAlignments & Alignment.Foe) == Alignment.None)
                yield break;

            //original target char needs to be a friend of the target char
            if ((DungeonScene.Instance.GetMatchup(ownerChar, targetChar) & DrawFrom) == Alignment.None)
                yield break;

            CharAnimSpin spinAnim = new CharAnimSpin();
            spinAnim.CharLoc = ownerChar.CharLoc;
            spinAnim.CharDir = ownerChar.CharDir;
            spinAnim.MajorAnim = true;

            yield return CoroutineManager.Instance.StartCoroutine(ownerChar.StartAnim(spinAnim));
            yield return new WaitWhile(ownerChar.OccupiedwithAction);

            DungeonScene.Instance.LogMsg(String.Format(Msg.ToLocal(), ownerChar.Name, owner.GetName()));
            context.ExplosionTile = ownerChar.CharLoc;
            context.Explosion.Range = 0;
            context.ContextStates.Set(new Redirected());
        }
    }

    [Serializable]
    public class PassAttackEvent : BattleEvent
    {
        public PassAttackEvent() { }
        public override GameEvent Clone() { return new PassAttackEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ContextStates.Contains<Redirected>())
                yield break;

            if (context.ActionType == BattleActionType.Trap || context.ActionType == BattleActionType.Item)
                yield break;

            //needs to be an attacking move
            if (context.Data.Category != BattleData.SkillCategory.Physical && context.Data.Category != BattleData.SkillCategory.Magical)
                yield break;

            if (ZoneManager.Instance.CurrentMap.GetCharAtLoc(context.ExplosionTile) != ownerChar)
                yield break;
            
            foreach (Character newTarget in ZoneManager.Instance.CurrentMap.IterateCharacters())
            {
                if (!newTarget.Dead && newTarget != ownerChar && newTarget != context.User
                    && (newTarget.CharLoc - ownerChar.CharLoc).Dist8() <= 1)
                {
                    CharAnimSpin spinAnim = new CharAnimSpin();
                    spinAnim.CharLoc = ownerChar.CharLoc;
                    spinAnim.CharDir = ownerChar.CharDir;
                    spinAnim.MajorAnim = true;

                    yield return CoroutineManager.Instance.StartCoroutine(ownerChar.StartAnim(spinAnim));
                    yield return new WaitWhile(ownerChar.OccupiedwithAction);

                    DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_PASS_ATTACK").ToLocal(), ownerChar.Name, newTarget.Name));
                    context.ExplosionTile = newTarget.CharLoc;
                    context.ContextStates.Set(new Redirected());
                    yield break;
                }
            }
            
        }
    }

    [Serializable]
    public class CoverAttackEvent : BattleEvent
    {
        public CoverAttackEvent() { }
        public override GameEvent Clone() { return new CoverAttackEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ContextStates.Contains<Redirected>())
                yield break;

            if (context.ActionType == BattleActionType.Trap || context.ActionType == BattleActionType.Item)
                yield break;

            Character targetChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(context.ExplosionTile);
            if (targetChar == null)
                yield break;

            if (targetChar.HP > targetChar.MaxHP / 2)
                yield break;

            //char needs to be a friend of the target char
            if (DungeonScene.Instance.GetMatchup(ownerChar, targetChar) != Alignment.Friend)
                yield break;

            CharAnimSpin spinAnim = new CharAnimSpin();
            spinAnim.CharLoc = ownerChar.CharLoc;
            spinAnim.CharDir = ownerChar.CharDir;
            spinAnim.MajorAnim = true;

            yield return CoroutineManager.Instance.StartCoroutine(ownerChar.StartAnim(spinAnim));
            yield return new WaitWhile(ownerChar.OccupiedwithAction);

            DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_COVER_ATTACK").ToLocal(), ownerChar.Name));
            context.ExplosionTile = ownerChar.CharLoc;
            context.ContextStates.Set(new Redirected());        
        }
    }



    [Serializable]
    public class FollowUpEvent : InvokeBattleEvent
    {
        public int InvokedMove;
        public bool AffectTarget;
        public int FrontOffset;
        public StringKey Msg;

        public FollowUpEvent() { }
        public FollowUpEvent(int invokedMove, bool affectTarget, int frontOffset, StringKey msg)
        {
            InvokedMove = invokedMove;
            AffectTarget = affectTarget;
            FrontOffset = frontOffset;
            Msg = msg;
        }
        protected FollowUpEvent(FollowUpEvent other)
        {
            InvokedMove = other.InvokedMove;
            AffectTarget = other.AffectTarget;
            FrontOffset = other.FrontOffset;
            Msg = other.Msg;
        }
        public override GameEvent Clone() { return new FollowUpEvent(this); }
        
        protected override BattleContext CreateContext(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            int damage = context.GetContextStateInt<DamageDealt>(0);
            if (damage > 0 && ownerChar != context.User)
            {
                //the attack needs to face the foe, and *auto-target*
                Dir8 attackDir = DirExt.GetDir(ownerChar.CharLoc, target.CharLoc);
                ownerChar.CharDir = attackDir;
                Loc frontLoc = ownerChar.CharLoc + attackDir.GetLoc() * FrontOffset;

                SkillData entry = DataManager.Instance.GetSkill(InvokedMove);

                DungeonScene.Instance.LogMsg(String.Format(Msg.ToLocal(), ownerChar.Name, context.User.Name));

                BattleContext newContext = new BattleContext(BattleActionType.Skill);
                newContext.User = ownerChar;
                newContext.UsageSlot = BattleContext.FORCED_SLOT;

                newContext.StartDir = newContext.User.CharDir;

                //fill effects
                newContext.Data = new BattleData(entry.Data);
                newContext.Data.ID = InvokedMove;
                newContext.Explosion = new ExplosionData(entry.Explosion);
                newContext.HitboxAction = entry.HitboxAction.Clone();
                //make the attack *autotarget*; set the offset to the space between the front loc and the target
                newContext.HitboxAction.HitOffset = target.CharLoc - frontLoc;
                newContext.Strikes = entry.Strikes;
                newContext.Item = new InvItem();
                //don't set move message, just directly give the message of what the move turned into

                //add a tag that will allow the moves themselves to switch to their offensive versions
                newContext.ContextStates.Set(new FollowUp());


                return newContext;
            }

            return null;
        }
    }

}
