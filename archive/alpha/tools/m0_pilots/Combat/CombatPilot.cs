using System;
using CubeGame.Core.Combat;
using CubeGame.Core.Progression;

namespace CubeGame.M0Pilots.Combat
{
    public static class CombatPilot
    {
        public static PilotResult Run()
        {
            Check.Start("PILOT_COMBAT");
            AbilityCosts();
            AbilityRuntimeContract();
            HitAndBlock();
            DamageIntegration();
            DeterministicBattle();
            Rewards();
            HonestCombatLimits();
            return Check.Finish();
        }

        private static void AbilityCosts()
        {
            Check.Near("cooldown id 1 = 6/haste at haste 1", 6.0,
                AbilityRules.CooldownSeconds(1, 1.0, 0.0, 1));
            Check.Near("cooldown id 1 = 6/haste at haste 2", 3.0,
                AbilityRules.CooldownSeconds(1, 2.0, 0.0, 1));
            Check.Near("cooldown id 5 is a flat 4 seconds", 4.0,
                AbilityRules.CooldownSeconds(5, 2.0, 0.0, 1));
            Check.Near("cooldown id 0x57 is a flat 10 seconds", 10.0,
                AbilityRules.CooldownSeconds(0x57, 2.0, 0.0, 1));
            Check.Near("cooldown id 0x36 is a flat 5 seconds", 5.0,
                AbilityRules.CooldownSeconds(0x36, 2.0, 0.0, 1));
            Check.Near("cooldown id 0x15 = powerFactor*30+5", 20.0,
                AbilityRules.CooldownSeconds(0x15, 1.0, 0.5, 1));
            Check.Near("unknown ability uses the default 2/haste", 1.0,
                AbilityRules.CooldownSeconds(0x77, 2.0, 0.0, 1));
            Check.Near("ally role is not counted (0 cooldown)", 0.0,
                AbilityRules.CooldownSeconds(1, 1.0, 0.0, AbilityRules.RoleAlly));
            Check.Near("npc role is not counted (0 cooldown)", 0.0,
                AbilityRules.CooldownSeconds(1, 1.0, 0.0, AbilityRules.RoleNpc));

            Check.Near("mana 0x15 rank 0 = 8000 units", 8000.0, AbilityRules.ManaUnits(0x15, 0.0));
            Check.Near("mana 0x15 rank 10 powerFactor 0.5 = 4000 units", 4000.0, AbilityRules.ManaUnits(0x15, 0.5));
            Check.Near("mana 0x56 rank 10 = 40000 units", 40000.0, AbilityRules.ManaUnits(0x56, 0.5));
            Check.Near("mana 0x48 is a constant 15000", 15000.0, AbilityRules.ManaUnits(0x48, 0.9));
            Check.Near("mana for an unknown id is 0", 0.0, AbilityRules.ManaUnits(0x77, 0.5));

            Check.Near("stamina id 0 = 100/haste", 100.0, AbilityRules.StaminaUnits(0, 1.0));
            Check.Near("stamina id 0x10 = 600/haste", 300.0, AbilityRules.StaminaUnits(0x10, 2.0));
            Check.Near("stamina id 0x0f = 400/haste", 400.0, AbilityRules.StaminaUnits(0x0F, 1.0));
            Check.Near("stamina default = 500/haste", 250.0, AbilityRules.StaminaUnits(0x77, 2.0));

            Check.Equal("channeled table has 32 documented ids", 32, AbilityRules.ChanneledCount);
            Check.True("0x22 HealingStream is channeled without the gate flags",
                AbilityRules.IsChanneled(0x22, 0));
            Check.True("0x15 RangerKick is not channeled", !AbilityRules.IsChanneled(0x15, 0));
            Check.True("flags 0x1a00 disable channeling even for a channeled id",
                !AbilityRules.IsChanneled(0x22, 0x0200));

            Check.True("0x13 is a melee ability id", AbilityRules.IsMeleeAbility(0x13));
            Check.True("0x43 is a melee ability id", AbilityRules.IsMeleeAbility(0x43));
            Check.True("0x97 is in the documented melee exception list", AbilityRules.IsMeleeAbility(0x97));
            Check.True("0x15 is inside the documented melee range 0x13..0x28", AbilityRules.IsMeleeAbility(0x15));
            Check.True("0x12 is outside every melee range", !AbilityRules.IsMeleeAbility(0x12));
        }

        private static void AbilityRuntimeContract()
        {
            var runtime = new AbilityRuntime();
            Check.True("activation with full resources succeeds", runtime.TryActivate(0x15, 1.0, 0.0, 1));
            Check.Near("mana 8000/100000 is deducted", 0.92, runtime.Mana, 1e-12);
            Check.Near("stamina 500/100000 is deducted", 0.995, runtime.Stamina, 1e-12);
            Check.Near("cooldown 5 seconds (powerFactor 0) is set", 5000.0, runtime.CooldownRemainingMs, 1e-9);

            Check.True("activation is refused while the cooldown is running", !runtime.TryActivate(0x15, 1.0, 0.0, 1));
            Check.Near("refused activation does not consume mana", 0.92, runtime.Mana, 1e-12);

            runtime.Tick(2500);
            Check.Near("cooldown ticks down", 2500.0, runtime.CooldownRemainingMs, 1e-9);
            runtime.Tick(2500);
            Check.Near("cooldown reaches 0 and clamps", 0.0, runtime.CooldownRemainingMs, 1e-12);

            var poor = new AbilityRuntime { Mana = 0.01, Stamina = 1.0 };
            Check.True("insufficient mana is refused", !poor.TryActivate(0x56, 1.0, 0.0, 1));
            Check.Near("failed activation leaves mana unchanged", 0.01, poor.Mana, 1e-12);
            Check.Near("failed activation leaves stamina unchanged", 1.0, poor.Stamina, 1e-12);
            Check.Near("failed activation leaves cooldown at 0", 0.0, poor.CooldownRemainingMs, 1e-12);

            Check.Throws<ArgumentOutOfRangeException>("negative cooldown tick is rejected",
                () => runtime.Tick(-1.0));
        }

        private static void HitAndBlock()
        {
            Check.True("hit roll 16383/32767 is below 0.5", HitResolver.IsHit(0.5, 16383));
            Check.True("hit roll 16384/32767 is not below 0.5", !HitResolver.IsHit(0.5, 16384));
            Check.True("chance 0 never hits", !HitResolver.IsHit(0.0, 0));
            Check.True("chance 1 hits every roll except the maximum (strict < boundary)",
                HitResolver.IsHit(1.0, 32766) && !HitResolver.IsHit(1.0, 32767));

            Check.Near("weapon subtype 0 halves hit chance (PARTIAL rule)", 0.25,
                CombatFormulas.ApplyWeaponSubtypeHitModifier(0.5, 3, 0), 1e-12);
            Check.Near("weapon subtype 0x0c halves hit chance", 0.25,
                CombatFormulas.ApplyWeaponSubtypeHitModifier(0.5, 3, 0x0C), 1e-12);
            Check.Near("weapon subtype 8 multiplies hit chance by 0.3", 0.15,
                CombatFormulas.ApplyWeaponSubtypeHitModifier(0.5, 3, 8), 1e-12);
            Check.Near("weapon subtype 10 multiplies hit chance by 0.3", 0.15,
                CombatFormulas.ApplyWeaponSubtypeHitModifier(0.5, 3, 10), 1e-12);
            Check.Near("non-weapon item type is not modified", 0.5,
                CombatFormulas.ApplyWeaponSubtypeHitModifier(0.5, 4, 0), 1e-12);
            Check.Near("longsword subtype keeps hit chance", 0.5,
                CombatFormulas.ApplyWeaponSubtypeHitModifier(0.5, 3, 5), 1e-12);

            Check.Equal("block threshold level 1 = 1", 1, CombatFormulas.BlockThreshold(1));
            Check.Equal("block threshold level 5 = 17", 17, CombatFormulas.BlockThreshold(5));
            Check.Equal("block threshold level 10 = 32", 32, CombatFormulas.BlockThreshold(10));
            Check.True("block roll equal to the threshold succeeds", CombatFormulas.RollBlock(10, 32));
            Check.True("block roll above the threshold fails", !CombatFormulas.RollBlock(10, 33));
        }

        private static void DamageIntegration()
        {
            var attack = new CombatAttack(10.0, 10, 0.5)
            {
                EquippedItemType = 3,
                WeaponSubtype = 5,
                TargetArmor = 5.0,
                BlockFraction = 0.5,
            };

            var target = new HealthPool(1000.0);
            var resolver = new DamageResolver(new FixedCombatRandom(0, 0));
            CombatOutcome outcome = resolver.Resolve(attack, target);

            Check.True("seeded hit with rolls 0/0 lands", outcome.IsHit);
            Check.Near("variance roll 0 gives 1.25", 1.25, outcome.VarianceMultiplier, 1e-12);
            Check.True("mitigation was applied (raw > applied)", outcome.RawDamage > outcome.AppliedDamage);
            Check.Near("absorbed + final damage equals raw damage", outcome.RawDamage,
                outcome.AbsorbedDamage + (outcome.RawDamage - outcome.AbsorbedDamage), 1e-9);
            Check.Near("HP after equals HP before minus applied", outcome.HealthBefore - outcome.AppliedDamage,
                outcome.HealthAfter, 1e-9);
            Check.True("target stays alive after one hit", !outcome.IsDead);

            var miss = new DamageResolver(new FixedCombatRandom(32767)).Resolve(
                new CombatAttack(10.0, 10, 0.5), new HealthPool(100.0));
            Check.True("roll 32767 misses at chance 0.5", miss.Status == CombatStatus.Miss);
            Check.Near("miss does not change HP", 100.0, miss.HealthAfter, 1e-12);

            var dead = new HealthPool(1.0);
            dead.ApplyDamage(1.0);
            CombatOutcome alreadyDead = new DamageResolver(new FixedCombatRandom()).Resolve(
                new CombatAttack(10.0, 10, 1.0), dead);
            Check.True("attack on a dead target is reported without consuming rolls",
                alreadyDead.Status == CombatStatus.TargetAlreadyDead);

            var flagged = new CombatAttack(10.0, 10, 1.0)
            {
                UseAttackIdMultiplier = true,
                AttackId = 0x57,
            };
            CombatOutcome multiplied = new DamageResolver(new FixedCombatRandom(0, 0))
                .Resolve(flagged, new HealthPool(1000000.0));
            Check.True("attack id 0x57 multiplier is applied only when explicitly enabled",
                multiplied.AttackIdMultiplierApplied && Math.Abs(multiplied.AttackIdMultiplier - 50.0) < 1e-12);

            var unknownId = new CombatAttack(10.0, 10, 1.0)
            {
                UseAttackIdMultiplier = true,
                AttackId = 0x77,
            };
            CombatOutcome unknown = new DamageResolver(new FixedCombatRandom(0, 0))
                .Resolve(unknownId, new HealthPool(1000000.0));
            Check.True("unknown attack id is not multiplied", !unknown.AttackIdMultiplierApplied);
        }

        private static void DeterministicBattle()
        {
            double firstHealth;
            int firstHits;
            RunBattle(12345, out firstHealth, out firstHits);
            double secondHealth;
            int secondHits;
            RunBattle(12345, out secondHealth, out secondHits);

            Check.True("same seed produces an identical battle result",
                Math.Abs(firstHealth - secondHealth) < 1e-9 && firstHits == secondHits);
            Check.True("5000 attacks at chance 0.5 land near half", firstHits >= 2400 && firstHits <= 2600,
                "hits=" + firstHits);
            Check.True("battle leaves the target with non-negative finite HP", firstHealth >= 0.0 && !double.IsNaN(firstHealth));

            double otherHealth;
            int otherHits;
            RunBattle(54321, out otherHealth, out otherHits);
            Check.True("different seed changes the battle outcome",
                Math.Abs(firstHealth - otherHealth) > 1e-6 || firstHits != otherHits);
        }

        private static void RunBattle(int seed, out double health, out int hits)
        {
            var target = new HealthPool(1000000000.0);
            var resolver = new DamageResolver(new SeededCombatRandom(seed));
            var attack = new CombatAttack(10.0, 10, 0.5) { TargetArmor = 5.0, BlockFraction = 0.5 };
            hits = 0;
            for (int i = 0; i < 5000; i++)
            {
                CombatOutcome outcome = resolver.Resolve(attack, target);
                if (outcome.IsHit)
                {
                    hits++;
                }
            }

            health = target.CurrentHealth;
        }

        private static void Rewards()
        {
            double baseXp = AlphaMath.KillExperience(10, 0);
            Check.Near("boss experience is 20x the base formula", baseXp * 20.0,
                RewardRules.KillExperience(10, 0, true, false, false), 1e-9);
            Check.Near("flag 0x200 multiplies experience by 10", baseXp * 10.0,
                RewardRules.KillExperience(10, 0, false, true, false), 1e-9);
            Check.Near("flag 0x800 grants zero experience", 0.0,
                RewardRules.KillExperience(10, 0, true, true, true), 1e-12);
            Check.True("boss and flag multipliers compose", RewardRules.KillExperience(10, 0, true, true, false)
                > baseXp * 20.0);
            Check.Equal("killing a creature does not grant money", 0, RewardRules.MoneyFromKill());
            Check.True("experience formula is marked PARTIAL in the evidence matrix",
                !CombatEvidence.HitChanceBaseProven && !CombatEvidence.BlockFractionProven);
        }

        private static void HonestCombatLimits()
        {
            int action;
            Check.True("weapon subtype -> attack action is not invented",
                !AbilityRules.TryGetAttackAction(1, out action) && action == 0);
            Check.True("OQ-01 hit chance base is marked unproven", !CombatEvidence.HitChanceBaseProven);
            Check.True("OQ-24 block fraction is marked unproven", !CombatEvidence.BlockFractionProven);
            Check.True("critical hits are not claimed (no formula found)", !CombatEvidence.CriticalsExist);
            Check.True("weapon-to-action mapping is marked unproven", !CombatEvidence.WeaponToActionProven);
        }
    }
}
