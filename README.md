# Multi-Agent Squirrel Foraging Simulation

A reinforcement learning simulation of squirrel foraging behavior in a 3D terrain environment, built with Unity 6 and Unity ML-Agents. The agent uses a **homeostatic reward function** — behavior emerges from internal state maintenance rather than hand-crafted reward shaping.

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

To resume or fine-tune from a previous run:
```bash
mlagents-learn Assets/ML-Agents/Config/squirrel_ppo.yaml --run-id=run2 --initialize-from=run1 --time-scale 50
```

## Loading a Trained Model

1. Copy the `.onnx` file from `results/<run-id>/Squirrel/` into `Assets/ML-Agents/`
2. Select the `Squirrel` GameObject in the Hierarchy
3. In Inspector → **Behavior Parameters → Model**: drag in the `.onnx` file
4. Set **Behavior Type** to `Inference Only`
5. Press Play

Pre-trained models are in `Assets/ML-Agents/`. The project explores two reward formulations:
- **Explicit reward shaping** — direct rewards for acorn collection and safe-zone entry
- **Homeostatic reward** — reward derived entirely from internal state (hunger, energy, fear); behaviors emerge without hand-coded incentives

## Monitoring Training

```bash
conda activate mlagents
tensorboard --logdir results
```

Open `http://localhost:6006` in your browser.

## Reward Design

The squirrel maintains three internal state variables:

| Variable | Range | Meaning |
|----------|-------|---------|
| Hunger `H` | 0–1 | 1 = starving |
| Energy `E` | 0–1 | 1 = full energy |
| Fear `F` | 0–1 | 1 = terrified |

Per-step reward is computed from overall wellbeing:

```
W = (1 - H) * w_h  +  E * w_e  +  (1 - F) * w_f
reward = W * scale * 0.0003  +  survivalBonus  -  stepPenalty
```

No explicit reward is given for collecting acorns or entering safe zones — these behaviors emerge because they improve internal state:
- Acorn collected → hunger decreases → wellbeing increases
- Safe zone entered → fear decays 5× faster → wellbeing increases
- Predator nearby → fear increases → wellbeing decreases

## Evaluation Results (50 episodes)

| Metric | Explicit Reward | Homeostatic Reward |
|--------|----------------|--------------------|
| Avg Acorns Collected | 9.04 | 14.28 |
| Avg Survival Time (s) | 57.87 | 142.7 |
| Catch Rate | 72% | 34% |
| Avg SafeZone Entries | 0.70 | 2.16 |

The homeostatic formulation outperforms explicit reward shaping on all metrics. SafeZone-seeking behavior emerges without any direct reward signal for safe-zone entry.

## Metrics Recording

Episode metrics are written to `Metrics/results.csv` automatically during Play mode.  
Columns: `Episode, AcornsCollected, TotalDistance, Collisions, SafeZoneEntries, SurvivalTime, CaughtByPredator, FinalHunger, FinalEnergy, FinalFear, CumulativeReward`

To speed up evaluation, set `Eval Time Scale` on the `SimulationManager` GameObject in the Inspector (e.g. 20). Reset to 1 after evaluation.

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
    │   ├── Acorn.cs
    │   ├── AcornSpawner.cs            # maxAcorns configurable
    │   ├── Obstacle.cs                # Rock / DeepPit / Trash / MudPuddle
    │   └── SafeZone.cs
    └── Managers/
        ├── SimulationManager.cs
        └── MetricsRecorder.cs         # CSV episode logging
Metrics/
└── results.csv                        # evaluation output
results/
├── run11/                             # training checkpoints
└── run12/                             # ablation checkpoints
```

## Credits

Squirrel 3D model: "Low poly squirrel" by ClydeXYZ on Sketchfab (CC Attribution)
