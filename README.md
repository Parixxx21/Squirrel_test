# Multi-Agent Squirrel Foraging Simulation

A reinforcement learning simulation of squirrel foraging behavior in a 3D terrain environment, built with Unity 6 and Unity ML-Agents.

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
mlagents-learn Assets/ML-Agents/Config/squirrel_ppo.yaml --run-id=run1 --time-scale 20
```

Then press **Play** in Unity Editor after seeing:
```
[INFO] Listening on port 5004
```

## Loading a Trained Model

1. Copy the `.onnx` file from `results/<run-id>/Squirrel/` into `Assets/ML-Agents/`
2. Select the `Squirrel` GameObject in the Hierarchy
3. In Inspector → **Behavior Parameters → Model**: drag in the `.onnx` file
4. Set **Behavior Type** to `Inference Only`
5. Press Play

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
│   └── Config/squirrel_ppo.yaml   # PPO training config
├── Prefabs/
│   └── Acorn.prefab
├── Scenes/
│   └── ForagingScene.unity
└── Scripts/
    ├── Agents/
    │   ├── SquirrelAgent.cs        # RL agent (observations, actions, rewards)
    │   └── SquirrelAnimator.cs     # Procedural animation
    ├── Environment/
    │   ├── Acorn.cs
    │   ├── AcornSpawner.cs
    │   └── SafeZone.cs
    └── Managers/
        ├── SimulationManager.cs
        └── MetricsRecorder.cs
```

## TODO / Roadmap

### Environment
- [ ] Expand terrain size (currently 50×50, consider 100×100 or larger)
- [ ] Add obstacles (rocks, trees) with Obstacle tag
- [ ] Add safe zones (tree hollows, bushes) with Safezone tag
- [ ] Add terrain textures (grass, dirt, rock)
- [ ] Add trees using Unity Terrain tree system

### Agent & RL
- [ ] Multi-agent expansion (3–5 squirrels in same scene)
- [ ] Fear mechanic: nearby squirrels increase fear, triggering safe-zone seeking
- [ ] Memory (LSTM): add to squirrel_ppo.yaml so agent remembers past positions
- [ ] Increase vision radius (currently 10m) to match larger terrain
- [ ] Rule-based baseline agent for comparison

### Training & Experiments
- [ ] Continue training run4 to 1M steps (currently ~400k)
- [ ] Run experiments with different acorn densities
- [ ] Run experiments with different number of agents
- [ ] Record metrics: acorns collected, travel distance, collisions, energy use

### Visualization & Demo
- [ ] Add squirrel animations (walk/idle cycles)
- [ ] Add visual indicators for hunger/energy/fear (UI bars or color changes)
- [ ] Record demo video of trained behavior
- [ ] TensorBoard reward curve plots for report

### Report
- [ ] Methods section: environment design, observation space, reward function
- [ ] Results section: training curves, behavior analysis
- [ ] Comparison: RL vs rule-based baseline

## Credits

Squirrel 3D model: "Low poly squirrel" by ClydeXYZ on Sketchfab (CC Attribution)
