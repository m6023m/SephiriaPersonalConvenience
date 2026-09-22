"""Generate executable catalog and native pipeline cases, never accuracy verdicts.
Equipment-specific formula accuracy and visual QA require separate cases/evidence.
"""
import csv
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'PersonalConvenience/evidence/damage-details/validation-cases.json'
GAME_DATA = Path(r'C:\Program Files (x86)\Steam\steamapps\common\Sephiria\Sephiria_Data')

def symbol(name):
    return {'path': 'PersonalConvenience/DamageTooltip.cs', 'symbol': name}

common = [symbol('Snapshot'), symbol('Stats'), symbol('Capture'), 'PersonalConvenience/DamageTooltip.cs', symbol('FinishOutgoing'),
          'PersonalConvenience/DamageStateInvalidation.cs',
          'PersonalConvenience/ConditionalDamageProfiles.cs',
          'work/autoparry/game-source/Charm_TooCloseDamage.cs',
          'work/autoparry/game-source/Charm_FirstAttackBonusDamage.cs',
          'work/autoparry/game-source/Charm_DebuffDamage.cs',
          'work/autoparry/game-source/Charm_PointedBat.cs',
          'work/autoparry/game-source/Charm_WarmGlove.cs',
          'PersonalConvenience/FullBuffPlan.cs',
          'PersonalConvenience/BuffStatProjection.cs',
          'PersonalConvenience/BuffStatRule.cs',
          'work/autoparry/game-source/StatusDatabase.cs',
          'PersonalConvenience/BuffStatusMappings.cs',
          'PersonalConvenience/MagicDamageProfiles.cs',
          'work/autoparry/game-source/CharacterBuff_LightningArmor.cs',
          'work/autoparry/game-source/ActiveSkill_Buff.cs',
          'PersonalConvenience/WeaponBuffPreview.cs',
          'work/autoparry/game-source/WeaponSimple_Katana.cs',
          'work/autoparry/game-source/WeaponSimple_GreatSword.cs',
          'work/autoparry/game-source/WeaponAddonGreatsword_BoneBlood.cs',
          'work/autoparry/game-source/CharacterBuff.cs',
          'work/autoparry/game-source/SkillController.cs',
          'work/autoparry/damage-catalog/native-cast-timing.json',
          'PersonalConvenience/FullBuffCastTiming.cs',
          'PersonalConvenience/FullBuffMpRecovery.cs',
          'PersonalConvenience/FullBuffHpRecovery.cs',
          'PersonalConvenience/FullBuffHealthTrigger.cs',
          'PersonalConvenience/FullBuffTuningFork.cs',
          'PersonalConvenience/FullBuffMpTrigger.cs',
          'PersonalConvenience/FullBuffMagicNumbers.cs',
          'PersonalConvenience/FullBuffWeaponCost.cs',
          'work/autoparry/game-source/CharacterBuff_Heal.cs',
          'work/autoparry/game-source/CharacterBuff_MPHeal.cs',
          'work/autoparry/game-source/WeaponSimple_Crossbow.cs',
          'work/autoparry/game-source/Charm_Magic.cs',
          'work/autoparry/game-source/Charm_MPMultipleCast.cs',
          'work/autoparry/game-source/Charm_WaterBag.cs',
          'work/autoparry/game-source/Charm_GainBuffOnMPLoss.cs',
          'work/autoparry/game-source/Charm_TuningForks.cs',
          'work/autoparry/game-source/Charm_IncreaseAllDamageByHP.cs',
          'work/autoparry/game-source/Charm_EnhancedPotionCork.cs',
          'PersonalConvenience/FullBuffSwordRecovery.cs',
          'PersonalConvenience/FullBuffDashRecovery.cs',
          'work/autoparry/game-source/CharacterDash.cs',
          'PersonalConvenience/DamageStatPreview.cs',
          'PersonalConvenience/DamageStatNumbers.cs',
          'PersonalConvenience/DamageResourceNumbers.cs',
          'PersonalConvenience/DamageTooltipUI.cs',
          'PersonalConvenience/SelectedDamageSmoke.cs',
          'PersonalConvenience/DamageTestAccess.cs',
          'PersonalConvenience/run-damage-tooltip-smoke.ps1',
          'PersonalConvenience/incremental-validation.py',
          'work/autoparry/game-source/DamageInstance.cs',
          'work/autoparry/game-source/UnitAvatar.cs',
          str(GAME_DATA / 'Managed/Assembly-CSharp.dll'),
          str(GAME_DATA / 'resources.assets'),
          str(GAME_DATA / 'sharedassets0.assets')]
weapon = [symbol('WeaponText'), symbol('WeaponAttacks'), symbol('ResourceAttacks'),
          symbol('SpecialHit'), symbol('MpAttackRange'), symbol('MagicBladeHit'),
          symbol('TempestHit'), symbol('TempestDescription'), symbol('CloudSlashRange'),
          'PersonalConvenience/WeaponBuffPreview.cs',
          'PersonalConvenience/WeaponAdditionalDamageProfiles.cs',
          'PersonalConvenience/MagicDamageProfiles.cs',
          'PersonalConvenience/FollowerDamageProfiles.cs',
          'PersonalConvenience/ProjectileDamageProfiles.cs']
artifact = [symbol('ArtifactText'), 'PersonalConvenience/ArtifactDamageProfiles.cs',
            'PersonalConvenience/BasicArtifactProfiles.cs',
            'work/autoparry/game-source/AutoMagicCaster.cs',
            'work/autoparry/game-source/Charm_Magic.cs',
            'PersonalConvenience/ResourceArtifactProfiles.cs',
            'PersonalConvenience/RecoveryArtifactProfiles.cs',
            'PersonalConvenience/BuffStatProjection.cs',
            'PersonalConvenience/GrowthArtifactProfiles.cs',
            'PersonalConvenience/ChargingDamageProfiles.cs',
            'PersonalConvenience/MagicDamageProfiles.cs',
            'PersonalConvenience/FollowerDamageProfiles.cs',
            'PersonalConvenience/PlanetDamageProfiles.cs',
            'PersonalConvenience/ProjectileDamageProfiles.cs']
cases = []
for row in csv.DictReader((OUT.parent / 'equipment-coverage.csv').open(encoding='utf-8-sig')):
    if row['in_scope'] != 'True':
        continue
    category, identifier = row['id'].split(':')
    cases.append({'id': 'catalog:' + row['id'], 'kind': 'catalog',
                  'operation': category + '-catalog', 'item_id': int(identifier),
                  'name_ko': row['name_ko'],
                  'dependencies': common + (weapon if category == 'weapon' else artifact) +
                  [{'path': 'work/autoparry/damage-catalog/equipment-rules.json', 'json_key': row['id']}]})
for direct, elemental in [(True, False), (False, False), (False, True)]:
    for critical in (False, True):
        name = ('direct' if direct else 'elemental-effect' if elemental else 'none') + (':critical' if critical else ':normal')
        cases.append({'id': 'accuracy:pipeline:' + name, 'kind': 'accuracy',
                      'operation': 'native-pipeline', 'direct': direct,
                      'critical': critical, 'elemental_effect': elemental,
                      'dependencies': common})
        cases.append({'id': 'accuracy:pipeline:projectile-additions:' + name, 'kind': 'accuracy',
                      'operation': 'native-pipeline', 'direct': direct,
                      'critical': critical, 'elemental_effect': elemental,
                      'additional_damage': 7.5, 'projectile_percent': 35,
                      'dependencies': common})
        cases.append({'id': 'accuracy:pipeline:elite:' + name, 'kind': 'accuracy',
                      'operation': 'native-pipeline', 'direct': direct, 'elite': True,
                      'critical': critical, 'elemental_effect': elemental,
                      'additional_damage': 7.5, 'projectile_percent': 35,
                      'dependencies': common})
for direct, elemental in [(True, False), (False, False), (False, True)]:
    name = 'direct' if direct else 'elemental-effect' if elemental else 'none'
    cases.append({'id': 'accuracy:pipeline:execution:' + name, 'kind': 'accuracy',
                  'operation': 'native-pipeline', 'direct': direct, 'execution': True,
                  'elemental_effect': elemental, 'additional_damage': 7.5,
                  'projectile_percent': 35, 'dependencies': common})
for direct in (False, True):
    for multiplier in (0.5, 1.5, 2.0):
        for execution in (False, True):
            name = ('direct' if direct else 'none') + ':' + str(multiplier) + (':execution' if execution else ':critical')
            cases.append({'id': 'accuracy:pipeline:critical-order:' + name, 'kind': 'accuracy',
                          'operation': 'native-pipeline', 'direct': direct,
                          'critical': not execution, 'execution': execution,
                          'extra_critical': 11, 'element_critical': 20,
                          'critical_multiplier': multiplier,
                          'additional_damage': 7.5, 'projectile_percent': 35,
                          'dependencies': common})
for direct in (False, True):
    for outcome in ('normal', 'critical', 'execution'):
        for elite in (False, True):
            name = ('direct' if direct else 'none') + ':' + outcome + (':elite' if elite else '')
            cases.append({'id': 'accuracy:follower-pipeline:' + name, 'kind': 'accuracy',
                          'operation': 'native-follower-pipeline', 'direct': direct,
                          'critical': outcome == 'critical', 'execution': outcome == 'execution', 'elite': elite,
                          'extra_critical': 11, 'critical_multiplier': 1.5,
                          'additional_damage': 7.5, 'projectile_percent': 35,
                          'dependencies': common + ['PersonalConvenience/FollowerDamageProfiles.cs']})
for force_chaos in (False, True):
    for direct in (False, True):
        for execution in (False, True):
            cases.append({'id': 'accuracy:follower-element:' + ('chaos' if force_chaos else 'physical') + (':direct' if direct else ':none') + (':execution' if execution else ':critical'),
                          'kind': 'accuracy', 'operation': 'native-follower-pipeline',
                          'force_chaos': force_chaos, 'direct': direct,
                          'critical': not execution, 'execution': execution,
                          'element_critical': 20, 'extra_critical': 11, 'critical_multiplier': 1.5,
                          'additional_damage': 7.5, 'projectile_percent': 35,
                          'dependencies': common + ['PersonalConvenience/FollowerDamageProfiles.cs']})
for heavy, inherit in ((False, False), (False, True), (True, False)):
    for direct in (False, True):
        name = ('heavy' if heavy else 'ordinary') + (':inherit' if inherit else ':override') + (':direct' if direct else ':none')
        cases.append({'id': 'accuracy:explosion:' + name, 'kind': 'accuracy',
                      'operation': 'native-explosion', 'heavy': heavy,
                      'inherit_element': inherit, 'direct': direct,
                      'dependencies': common + ['PersonalConvenience/ProjectileDamageProfiles.cs',
                          'work/autoparry/game-source/Bullet.cs',
                          'work/autoparry/game-source/BulletDestroyModule_Explode.cs',
                          'work/autoparry/game-source/BulletDestroyModule_HeavyExplode.cs']})
for amplified in (1.0, 2.5, 10.0):
    for critical in (False, True):
        cases.append({'id': 'accuracy:lightning-armor:' + str(amplified) + (':critical' if critical else ':normal'),
                      'kind': 'accuracy', 'operation': 'native-lightning-armor',
                      'buff_amplified': amplified, 'critical': critical,
                      'dependencies': common})
for addition in (0.01, 0.1, 0.33333334, 123.456):
    for operation in ('native-pipeline', 'native-follower-pipeline'):
        for critical in (False, True):
            cases.append({'id': 'accuracy:float-order:' + operation + ':' + str(addition) + (':critical' if critical else ':normal'),
                          'kind': 'accuracy', 'operation': operation,
                          'additional_damage': addition, 'projectile_percent': 35,
                          'direct': False, 'elemental_effect': operation == 'native-pipeline',
                          'critical': critical, 'dependencies': common + ['PersonalConvenience/FollowerDamageProfiles.cs']})
OUT.write_text(json.dumps({'cases': cases}, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'Wrote {len(cases)} executable cases. Equipment formula accuracy and UI remain separate.')
