﻿using System.Linq;
using CoreAutomata;

namespace FateGrandAutomata
{
    public class Battle
    {
        bool _hasTakenFirstStageSnapshot;

        public bool HasClickedAttack { get; private set; }

        public bool HasChoosenTarget { get; private set; }

        public int CurrentStage { get; private set; }
        public int CurrentTurn { get; private set; }

        public AutoSkill AutoSkill { get; private set; }

        public Card Card { get; private set; }

        public void Init(AutoSkill AutoSkillModule, Card CardModule)
        {
            AutoSkill = AutoSkillModule;
            Card = CardModule;

            ResetState();
        }

        public void ResetState()
        {
            AutoSkill.ResetState();

            CurrentStage = CurrentTurn = 0;

            _hasTakenFirstStageSnapshot = HasChoosenTarget = HasClickedAttack = false;
        }

        public bool IsIdle()
        {
            return Game.BattleScreenRegion.Exists(ImageLocator.Battle);
        }

        public void ClickAttack()
        {
            Game.BattleAttackClick.Click();

            // Although it seems slow, make it no shorter than 1 sec to protect user with less processing power devices.
            AutomataApi.Wait(1.5);

            HasClickedAttack = true;
        }

        void SkipDeathAnimation()
        {
            // https://github.com/29988122/Fate-Grand-Order_Lua/issues/55 Experimental
            for (var i = 0; i < 3; ++i)
            {
                Game.BattleSkipDeathAnimationClick.Click();

                AutomataApi.Wait(1);
            }
        }

        bool IsPriorityTarget(Region Target)
        {
            var isDanger = Target.Exists(ImageLocator.TargetDanger);
            var isServant = Target.Exists(ImageLocator.TargetServant);

            return isDanger || isServant;
        }

        void ChooseTarget(int Index)
        {
            Game.BattleTargetClickArray[Index].Click();

            AutomataApi.Wait(0.5);

            Game.BattleExtrainfoWindowCloseClick.Click();

            HasChoosenTarget = true;
        }

        void OnStageChanged()
        {
            ++CurrentStage;
            CurrentTurn = 0;
            HasChoosenTarget = false;
        }

        void AutoChooseTarget()
        {
            // from my experience, most boss stages are ordered like(Servant 1)(Servant 2)(Servant 3),
            // where(Servant 3) is the most powerful one. see docs/ boss_stage.png
            // that's why the table is iterated backwards.

            var i = 0;

            foreach (var target in Game.BattleTargetRegionArray.Reverse())
            {
                if (IsPriorityTarget(target))
                {
                    ChooseTarget(i);
                    return;
                }

                ++i;
            }
        }

        public void PerformBattle()
        {
            AutomataApi.UseSameSnapIn(OnTurnStarted);
            AutomataApi.Wait(2);

            if (Preferences.EnableAutoSkill)
            {
                AutoSkill.Execute();
            }

            if (!HasClickedAttack)
            {
                ClickAttack();
            }

            if (Card.CanClickNpCards)
            {
                Card.ClickNpCards();
            }

            Card.ClickCommandCards();

            if (Preferences.UnstableFastSkipDeadAnimation)
            {
                SkipDeathAnimation();
            }

            AutomataApi.Wait(2);
        }

        void OnTurnStarted()
        {
            CheckCurrentStage();

            ++CurrentTurn;

            HasClickedAttack = false;

            if (!HasChoosenTarget && Preferences.BattleAutoChooseTarget)
            {
                AutoChooseTarget();
            }
        }

        void CheckCurrentStage()
        {
            if (!_hasTakenFirstStageSnapshot || DidStageChange())
            {
                TakeStageSnapshot();

                OnStageChanged();
            }
        }

        IPattern _generatedStageCounterSnapshot;

        bool DidStageChange()
        {
            // Alternative fix for different font of stage count number among different regions, worked pretty damn well tho.
            // This will compare last screenshot with current screen, effectively get to know if stage changed or not.

            return !Game.BattleStageCountRegion.Exists(_generatedStageCounterSnapshot, Similarity: 0.7);
        }

        void TakeStageSnapshot()
        {
            _generatedStageCounterSnapshot = Game.BattleStageCountRegion.Save();

            _hasTakenFirstStageSnapshot = true;
        }
    }
}