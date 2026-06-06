# Scenario 4: Falling Tree Rescue

## Scenario Overview

This scenario simulates a falling tree accident during a night storm.  
A tree becomes unstable and falls near or onto a pedestrian using physics simulation. The pedestrian becomes injured, a witness reacts, and emergency services are called.

This scenario is part of the **Virtual Ambulance Emergency Response Simulation** project.

## Scenario Goal

The goal of this scenario is to demonstrate a physics-based accident using a falling tree.

The scenario focuses on:

1. Night or storm atmosphere.
2. Tree physics.
3. Patient injury.
4. Witness reaction.
5. Emergency call.
6. Rescue sequence start.


## Scene Flow

1. The scene starts at night or during a storm.
2. The pedestrian walks or stands near a tree.
3. The tree becomes unstable.
4. The tree fall is triggered.
5. The tree falls using physics simulation.
6. The tree collides with the ground or accident area.
7. The pedestrian becomes injured.
8. The witness reacts.
9. The witness calls emergency services.
10. The ambulance rescue sequence begins.
11. The paramedic reaches the patient.
12. The patient is stabilized and evacuated.
13. The ambulance leaves the scene.


## Characters Used

| Character | Role |
|---|---|
| Pedestrian / Patient | Accident victim |
| Witness | Reacts and calls emergency services |
| Paramedic / Doctor | Performs rescue |
| crowd NPCs | Add realism |


## Environment Assets

Scene objects:

- Street
- Sidewalk
- Buildings
- Tree
- Street lamps
- Rain or storm effects
- Wind audio
- Ambulance
- Debris or fallen branches


## Lighting

This scenario uses a night or storm lighting setup.

Recommended lighting types:

| Light Type | Usage |
|---|---|
| Directional Light | Moonlight or low night lighting |
| Spot Light | Street lamps |
| Point Light | Ambulance lights or building lights |
| Ambient Light | Low blue-gray nighttime mood |

Lighting should create a darker atmosphere than the daytime scenarios.

## Physics Implementation

The falling tree is the main physics element.

Tree setup:

Tree
├── Rigidbody
├── Collider
└── HingeJoint or pivot constraint
