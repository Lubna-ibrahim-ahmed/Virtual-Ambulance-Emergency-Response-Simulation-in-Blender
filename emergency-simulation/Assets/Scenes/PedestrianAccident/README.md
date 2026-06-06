# Scenario 2: Pedestrian Accident / Car Collision

## Scenario Overview

This scenario simulates a pedestrian accident in a city street.  
A pedestrian crosses the road while a car approaches. The accident sequence causes the pedestrian to become injured, debris or collision effects appear, and a witness reacts by calling emergency services.

This scenario is part of the **Virtual Ambulance Emergency Response Simulation** project.


## Scenario Goal

The goal of this scenario is to demonstrate a street car accident and prepare the scene for ambulance rescue.

The scenario focuses on:

1. Car movement.
2. Pedestrian crossing.
3. Collision event.
4. Patient injury.
5. Witness reaction.
6. Emergency call.
7. Rescue sequence start.

## Scene Flow

1. The scene starts in a daytime street environment.
2. The pedestrian is placed near or on the road.
3. A car approaches the pedestrian.
4. The car collision event is triggered.
5. The pedestrian becomes injured.
6. Debris or accident objects react using physics.
7. The witness reacts to the accident.
8. The witness calls emergency services.
9. A rescue request is sent.
10. The ambulance rescue sequence begins.
11. The paramedic reaches the patient.
12. The patient is evacuated by stretcher.
13. The ambulance leaves the scene.

## Characters Used

| Character | Role |
|---|---|
| Pedestrian / Patient | Accident victim |
| Witness | Reacts and calls emergency services |
| Paramedic / Doctor | Performs rescue sequence |


## Vehicles Used

| Vehicle | Role |
|---|---|
| Car | Causes or represents the accident |
| Ambulance | Responds to the emergency |

## Environment Assets

This scenario uses a street environment containing:

- Road
- Sidewalk
- Buildings
- Street lights
- Traffic cones or signs
- Car accident area
- Ambulance area
- Optional debris objects

## Physics Implementation

Physics was used for:

- Car movement
- Collision detection
- Debris scattering
- Impact reaction
- Rigidbody-based accident objects

Physics behavior:

Car starts from a defined position.
Car moves toward the pedestrian.
Collision trigger activates.
Debris receives impulse forces.
Witness reaction starts.
Rescue sequence begins.
