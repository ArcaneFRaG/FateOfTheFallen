# Varkesh and Gorath World-Boss Equipment — r20

This is the implemented late-world-boss equipment roster for v0.2. IDs 1821-1825 and 1831-1835 remain stable so earlier test-save copies upgrade in place.

## Global world-boss reward rule

All four FOTF world bosses now use two independent normal equipment rolls per kill.

- Each boss equipment table totals exactly **100%**.
- Each kill makes **two independent equipment rolls**.
- The second roll does **not** exclude the first result, so duplicate drops are valid.
- Example: Gorath can drop two Stormfires on the same kill.
- Quest progression items remain quest-only additive drops while their associated quest is active.

Current equipment-table totals:

- Moldermaw: 12 + 16 + 20 + 24 + 28 = **100%**
- Nhal'Sera: 14 + 16 + 18 + 24 + 28 = **100%**
- Varkesh: 12 + 16 + 20 + 24 + 28 = **100%**
- Gorath: 12 + 16 + 20 + 24 + 28 = **100%**

---

## Varkesh the Unburied — Death pool — Item Level 55 Relic tier

| Item | ID | Role | Slot | Drop chance per roll |
|---|---:|---|---|---:|
| Gravetide Reaver | 1822 | Paladin/Reaver DPS | PrimaryOrSecondary 1H Sword | 12% |
| Heart of the Deathless | 1823 | Paladin/Reaver Tank | Ring | 16% |
| Soulbinder's Grimoire | 1821 | Caster | Secondary Tome | 20% |
| Lantern of Departed Souls | 1824 | Druid Healer | Secondary | 24% |
| Band of the Gravewind | 1825 | Stormcaller | Ring | 28% |

### Soulbinder's Grimoire
STR 0 / END 24 / DEX 0 / AGI 6 / INT 88 / WIS 48 / CHA 28 / RES 16  
HP 900 / Mana 825 / Armor 120  
MR 18 / PR 12 / ER 12 / VR 24  
**Soul Rend:** 15% chance on damaging spell to deal 3,600 Magic damage and restore 180 Mana.

Icon: `FOTF_Soulbinders_Grimoire.png`  
Prefab: `FOTF_Soulbinders_Grimoire`

### Gravetide Reaver
STR 92 / END 34 / DEX 58 / AGI 26 / INT 0 / WIS 0 / CHA 0 / RES 12  
HP 1,400 / Mana 175 / Armor 160  
MR 14 / PR 12 / ER 12 / VR 24  
Weapon Damage 44 / Delay 1.15  
**Death's Momentum:** 10% chance on a one-handed physical weapon attack to deal 5,000 additional Physical damage and grant +12% attack speed for 6 seconds.

Icon: `FOTF_Gravetide_Reaver.png`  
Prefab: `FOTF_Gravetide_Reaver`

### Heart of the Deathless
STR 24 / END 78 / DEX 0 / AGI 0 / INT 10 / WIS 34 / CHA 0 / RES 20  
HP 1,550 / Mana 325 / Armor 140  
MR 20 / PR 20 / ER 18 / VR 26  
**Refuse Death:** when falling below 25% HP, gain +150 Armor, 15% damage reduction and +25% healing received for 8 seconds. 90 second cooldown.

Icon: `FOTF_Heart_of_the_Deathless.png`  
No equipped model: ring slot.

### Lantern of Departed Souls
STR 0 / END 28 / DEX 0 / AGI 6 / INT 56 / WIS 92 / CHA 36 / RES 16  
HP 1,000 / Mana 950 / Armor 125  
MR 18 / PR 14 / ER 14 / VR 26  
**Soulkeeper's Grace:** directly healing an ally below 35% HP increases that heal by 25% and grants +10% healing for 6 seconds. 12 second internal cooldown.

Icon: `FOTF_Lantern_of_Departed_Souls.png`  
Prefab: `FOTF_Lantern_of_Departed_Souls`

### Band of the Gravewind
STR 34 / END 34 / DEX 70 / AGI 54 / INT 30 / WIS 12 / CHA 0 / RES 14  
HP 975 / Mana 350 / Armor 105  
MR 16 / PR 14 / ER 14 / VR 24  
**Gravewind:** successful ranged attacks have a 12% chance to grant Gravewind for 8 seconds: +12% ranged attack speed, +10 DEX and +10 AGI. While active, ranged attacks have a 20% chance to deal 1,200 Magic damage. 15 second internal cooldown.

Icon: `FOTF_Band_of_the_Gravewind.png`  
No equipped model: ring slot.

---

## Gorath the Ash-Crowned — Infernal pool — Item Level 60 Relic tier

| Item | ID | Role | Slot | Drop chance per roll |
|---|---:|---|---|---:|
| Stormfire | 1835 | Stormcaller | Primary Bow | 12% |
| Ember of the Demon King | 1831 | Caster | PrimaryOrSecondary Wand | 16% |
| Hellforged Talon | 1832 | Paladin/Reaver DPS | PrimaryOrSecondary 1H Axe | 20% |
| Infernal Bastion | 1833 | Paladin/Reaver Tank | Secondary Shield | 24% |
| Ashen Covenant | 1834 | Druid Healer | Ring | 28% |

### Ember of the Demon King
STR 0 / END 26 / DEX 0 / AGI 12 / INT 98 / WIS 38 / CHA 34 / RES 14  
HP 925 / Mana 875 / Armor 95  
MR 14 / PR 10 / ER 28 / VR 14  
Weapon Damage 66 / Delay 1.40 / Range 25  
**Hellfire:** 20% chance on damaging spell to deal 2,400 Elemental damage and apply 500 Elemental damage every 2 seconds for 8 seconds. Repeated procs refresh the duration and increase periodic damage by 10%, up to 3 additional stacks.

Icon: `FOTF_Ember_of_the_Demon_King.png`  
Prefab: `FOTF_Ember_of_the_Demon_King`

### Hellforged Talon
STR 100 / END 32 / DEX 62 / AGI 32 / INT 0 / WIS 0 / CHA 0 / RES 12  
HP 1,100 / Mana 175 / Armor 110  
MR 14 / PR 10 / ER 26 / VR 14  
Weapon Damage 48 / Delay 1.12  
**Infernal Frenzy:** 15% chance on physical weapon attack to deal 4,400 additional Physical damage and grant +10% attack speed for 6 seconds.

Icon: `FOTF_Hellforged_Talon.png`  
Prefab: `FOTF_Hellforged_Talon`

### Infernal Bastion
STR 40 / END 74 / DEX 16 / AGI 10 / INT 12 / WIS 24 / CHA 0 / RES 18  
HP 1,450 / Mana 350 / Armor 300  
MR 18 / PR 14 / ER 30 / VR 18  
**Demonic Challenge:** Bash has a 90% chance to taunt for 7,200 threat and grant Infernal Resolve, reducing damage taken by 5% for 6 seconds.

Icon: `FOTF_Infernal_Bastion.png`  
Prefab: `FOTF_Infernal_Bastion`

### Ashen Covenant
STR 0 / END 38 / DEX 0 / AGI 6 / INT 58 / WIS 90 / CHA 38 / RES 16  
HP 1,050 / Mana 975 / Armor 120  
MR 18 / PR 12 / ER 26 / VR 16  
**Ashen Covenant:** beneficial spells have a 15% chance to refund 30% of the spell's Mana cost and grant Ashen Grace, increasing healing by 8% for 6 seconds.

Icon: `FOTF_Ashen_Covenant.png`  
No equipped model: ring slot.

### Stormfire
STR 66 / END 30 / DEX 78 / AGI 48 / INT 74 / WIS 24 / CHA 16 / RES 12  
HP 1,100 / Mana 675 / Armor 115  
MR 16 / PR 10 / ER 30 / VR 14  
Weapon Damage 78 / Delay 1.25 / Range 30  
**Stormfire:** ranged attacks have a 20% chance to deal 2,600 Elemental damage and apply 475 Elemental damage every 2 seconds for 8 seconds. Repeated procs refresh and increase periodic damage by 10%, up to 3 additional stacks.  
**Arc Discharge:** spell damage against a target currently affected by the wearer's Stormfire has a 15% chance to deal 1,000 Elemental damage. 3 second internal cooldown.

Icon: `FOTF_Stormfire.png`  
Prefab: `FOTF_Stormfire`

---

## Visual implementation

The custom visual manager now targets these `fotf_equipment` prefabs for equipped-character visuals:

- `FOTF_Soulbinders_Grimoire`
- `FOTF_Gravetide_Reaver`
- `FOTF_Lantern_of_Departed_Souls`
- `FOTF_Ember_of_the_Demon_King`
- `FOTF_Hellforged_Talon`
- `FOTF_Infernal_Bastion`
- `FOTF_Stormfire`

The three rings are intentionally icon-only and have no equipped-character prefab:

- Heart of the Deathless
- Band of the Gravewind
- Ashen Covenant

The late-boss weapon/off-hand transform values are initial calibration values. They can be refined live with the existing `/fotfvisual` tooling after the approved prefabs are bundled into `fotf_equipment`.
