# Scenario 1: Gas Line Explosion Rescue

## Scenario Overview

This scenario simulates a gas line explosion accident in a city street.  
A gas leak appears near a roadwork area, then an explosion occurs. The explosion creates fire, smoke, and sound effects. A nearby patient falls to the ground, and a witness reacts to the accident.

This scenario is part of the **Virtual Ambulance Emergency Response Simulation** project.

## Scenario Goal

The goal of this scenario is to show the first emergency event in the project:

1. A gas leak starts.
2. An explosion happens.
3. A patient is injured.
4. A witness reacts and calls emergency services.
5. The scene becomes ready for ambulance rescue.

## Scene Flow

1. The street scene starts normally.
2. The gas leak area is placed near roadwork cones and barriers.
3. The player presses `G` to activate the gas vapor.
4. The gas vapor appears near the ground.
5. The player presses `X` to trigger the explosion.
6. The gas vapor disappears.
7. The explosion particle effect appears.
8. The explosion sound plays.
9. The patient falls to the ground.
10. The witness reacts by stepping backward.
11. Fire and smoke effects remain after the explosion.
12. Fire sound starts after the explosion.
13. The accident scene is ready for the ambulance rescue sequence.

## Characters Used

| Character | Role |
|---|---|
| Patient | Injured by the gas explosion |
| Witness | Reacts to the explosion and calls emergency services |

## Environment Assets

This scenario uses the shared city environment and includes:

- Street
- Sidewalk
- Buildings
- Street lights
- Ambulance
- Roadwork area
- Warning cones
- Gas leak zone
- Fire and smoke zone

## Main Objects

Hierarchy used:

GasLeakEffects
├── Gas_Vapor
├── Gas_Explosion
├── Fire_AfterExplosion
└── Smoke_AfterExplosion

ScenarioManager
└── ExplosionSequenceController

Character_Ch21
└── SimplePatientFall

Ch33
└── WitnessReaction

Patient_Fallen_Point
Witness_Back_Point
Explosion
Fire
