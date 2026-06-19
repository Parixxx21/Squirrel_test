# Multi-Agent Squirrel Foraging Simulation

A reinforcement learning simulation of squirrel foraging behavior in a 3D terrain environment, built with Unity 6 and Unity ML-Agents.

**Reward approach on this branch: State-Based Homeostatic Reward** — the squirrel agent receives no explicit reward for collecting acorns or entering safe zones. All behavior emerges from maintaining internal physiological state (hunger, energy, fear).

## Requirements

- Unity Hub + **Unity 6000.4.6f1** (version must match exactly)
- Python 3.10 (via Miniconda/Anaconda recommended)
- Git

## Project Setup

### 1. Clone the Repository

```bash
git clone <your-repo-url>
```

Open Unity Hub → **Add** → select the cloned project folder.  
Unity will regenerate the `Library/` folder on first launch — this takes a few minutes.

Open the scene: `Assets/Scenes/ForagingScene.unity`

### 2. Python Environment Setup

```bash
conda create -n mlagents python=3.10
conda activate mlagents

pip install git+https://github.com/Unity-Technologies/ml-agents.git@57d5307b54eac5edb142c1c7c762f0b5a95e4cec#subdirectory=ml-agents-envs
pip install git+https://github.com/Unity-Technologies/ml-agents.git@57d5307b54eac5edb142c1c7c762f0b5a95e4cec#subdirectory=ml-agents
pip install setuptools==65.5.1
```

### 3. Required Bug Fix (mlagents-envs compatibility)

Edit the file:
```
miniconda3/envs/mlagents/lib/python3.10/site-packages/mlagents_envs/environment.py
```

**Line 2** — replace:
```python
from distutils.version import StrictVersion
```
with:
```python
from packaging.version import Version as StrictVersion
```

**Lines 96–103** — replace all occurrences of:
- `.version[0]` → `.major`
- `.version[1]` → `.minor`

## Reward Design (State-Based Homeostatic)

The squirrel maintains three internal state variables updated every step:

| Variable | Range | Meaning |
|----------|-------|---------|
| Hunger `H` | 0–1 | 1 = starving |
| Energy `E` | 0–1 | 1 = full energy |
| Fear `F` | 0–1 | 1 = terrified |

Per-step reward is derived from overall wellbeing:

```
W = (1 - H) * w_h  +  E * w_e  +  (1 - F) * w_f
reward = W * wellbeingScale * 0.0003  +  survivalBonus  -  stepPenalty
```

Behaviors emerge naturally without explicit shaping:
- Acorn collected → hunger decreases → wellbeing increases
- Safe zone entered → fear decays 5× faster → wellbeing increases
- Predator nearby → fear increases → wellbeing decreases
- Obstacle collision → fear/energy penalized → wellbeing decreases

## Running Training

```bash
conda activate mlagents
cd <project-folder>
mlagents-learn Assets/ML-Agents/Config/squirrel_ppo.yaml --run-id=run1 --time-scale 50
```

Then press **Play** in Unity Editor after seeing:
```
[INFO] Listening on port 5004
```

To resume from a checkpoint:
```bash
mlagents-learn Assets/ML-Agents/Config/squirrel_ppo.yaml --run-id=run2 --initialize-from=run1 --time-scale 50
```

## Loading a Trained Model

1. Copy the `.onnx` file from `results/<run-id>/Squirrel/` into `Assets/ML-Agents/`
2. Select the `Squirrel` GameObject in the Hierarchy
3. In Inspector → **Behavior Parameters → Model**: drag in the `.onnx` file
4. Set **Behavior Type** to `Inference Only`
5. Press Play

Pre-trained models are available in `Assets/ML-Agents/`:
- `Squirrel-run12.onnx` — **final model** used for evaluation (homeostatic reward, acorns=15)
- `Squirrel-run11.onnx` — prior checkpoint (acorns=80)

## Monitoring Training

```bash
conda activate mlagents
tensorboard --logdir results
```

Open `http://localhost:6006` in your browser.

## Project Structure

```
Assets/
├── ML-Agents/
│   ├── Config/squirrel_ppo.yaml       # PPO training config
│   └── Squirrel-*.onnx                # trained model checkpoints
├── Scenes/
│   └── ForagingScene.unity
└── Scripts/
    ├── Agents/
    │   ├── SquirrelAgent.cs           # RL agent — homeostatic reward
    │   ├── SquirrelAnimator.cs        # procedural animation
    │   └── PredatorAgent.cs           # rule-based fox predator
    ├── Environment/
    │   ├── Acorn.cs / AcornSpawner.cs
    │   ├── Obstacle.cs / ObstacleSpawner.cs
    │   └── SafeZone.cs
    └── Managers/
        ├── SimulationManager.cs       # evalTimeScale field for fast evaluation
        └── MetricsRecorder.cs         # writes Metrics/results.csv
```

