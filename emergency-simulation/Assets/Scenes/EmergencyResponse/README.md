# 3. Emergency Response / Stretcher Rescue README.md

# Scenario 3: Emergency Response / Stretcher Rescue

## Scenario Overview

This scenario focuses on the ambulance rescue and patient evacuation sequence.  
It demonstrates the emergency response after an accident: the ambulance arrives, the paramedic reaches the patient, the patient is assessed, the stretcher is used, and the patient is loaded into the ambulance.

This scenario is part of the **Virtual Ambulance Emergency Response Simulation** project.

## Scenario Goal

The goal of this scenario is to show the full rescue process after an emergency event.

The scenario focuses on:

1. Ambulance arrival.
2. Paramedic movement.
3. Patient assessment.
4. Emergency care.
5. Stretcher evacuation.
6. Patient loading.
7. Ambulance departure.
8. Mission completion.

## Scene Flow

1. A rescue request is triggered from an accident scenario.
2. The ambulance receives the request.
3. Ambulance siren and flashing lights start.
4. The ambulance drives to the rescue location.
5. The ambulance stops at a safe distance.
6. The paramedic exits or appears near the ambulance.
7. The paramedic moves toward the patient.
8. The patient is assessed.
9. CPR or emergency care is performed if implemented.
10. The stretcher is moved to the patient.
11. The patient is placed on the stretcher.
12. The stretcher moves back to the ambulance.
13. The patient is loaded into the ambulance.
14. The ambulance doors close if implemented.
15. The ambulance leaves the accident scene.
16. Mission complete UI appears.

## Characters Used

| Character | Role |
|---|---|
| Patient | Injured person being rescued |
| Paramedic / Doctor | Main rescue character |


## Vehicles Used

| Vehicle | Role |
|---|---|
| Ambulance | Main rescue vehicle |


## Medical Objects

| Object | Purpose |
|---|---|
| Stretcher | Used to move the patient to the ambulance |
| Defibrillator | medical prop |
| Patient mount point | Position marker for patient on stretcher |


## Ambulance Features

The ambulance may include:

- Siren audio
- Flashing beacon lights
- Door objects
- Stop point
- Exit point
- Loading point
- Patient transport area

Recommended structure:

Ambulance
├── SirenLightLeft
├── SirenLightRight
├── Audio
└── AmbulanceLoadPoint
