# QueueLess AI Waiting-Time Prediction Methodology

## 1. Principles & Anti-Hallucination Guarantee

1. **No External LLM Hallucinations**: LLMs are never prompted to generate waiting times.
2. **No Random Number Generation**: Predictions are derived mathematically from physical queue mechanics and trained ML regression estimators.
3. **No False AI Marketing**: When a baseline mathematical calculation is returned, the system explicitly labels it as `BASELINE` ("Standard estimate based on queue position and configured consultation duration.").
4. **Data Isolation**: Patient-identifiable information is strictly excluded from training feature vectors.
5. **No Temporal Data Leakage**: Features available to the training algorithm contain only attributes known at prediction time. Future consultation completion timestamps are never leaked into inputs.

---

## 2. The 3 Prediction Modes

### Mode 1: Baseline Engine (New Hospital / Zero Data)
When a hospital registers, it possesses zero historical consultation records.
- Starting estimate source: Configured at the department level (`DefaultConsultationMinutes`) or hospital level (`InitialConsultationMinutes`).
- Calculation formula:
  $$\text{WaitTime} = \text{RemainingActiveServingMinutes} + (\text{PeopleAhead} \times \text{ConfiguredDuration})$$
- Excludes cancelled, completed, and no-show tokens from `PeopleAhead`.
- Provenance label: `BASELINE`.

### Mode 2: Multi-Hospital Shared Model
When sufficient authorized consultation records exist across participating hospitals (threshold: $\ge 40$ records):
- Trains an ML.NET FastTree / FastForest regression algorithm.
- Input Features:
  - Department category (one-hot encoded)
  - Time of day (hour)
  - Day of week (0 to 6)
  - Number of people ahead in queue
  - Current total queue workload
  - Active consultation remaining time estimate
  - Baseline duration estimate
- Label: Actual waiting time until consultation start.

### Mode 3: Hospital-Specific Calibrated Model
Once an individual hospital accumulates sufficient completed consultations (threshold: $\ge 20$ records):
- Trains an isolated ML.NET regression model strictly using that hospital's genuine completed consultation records.
- Chronological train/test split (80% training on older records, 20% validation on recent records).
- Acceptance Criterion:
  $$\text{Model MAE} \le \text{Baseline MAE} \times 1.15 \quad \text{and} \quad \text{Model MAE} < 60\text{ mins}$$
- Only models satisfying the acceptance criterion are promoted to active status.

---

## 3. Graceful Fallback Guarantee

If:
1. No trained model exists for the hospital,
2. Training record threshold is not met,
3. Model evaluation MAE exceeds acceptance criteria,
4. Model file is missing or corrupted on disk, or
5. Model produces an out-of-bounds or negative estimate,

The system instantly and seamlessly falls back to Mode 1 (Baseline calculation). Clinical queue operations are never blocked by model unavailability.

---

## 4. Synthetic Demo Data Separation

To allow reproducible evaluation on local developer environments without compromising clinical accuracy:
- The `SeedSyntheticConsultationDataAsync` utility generates realistic consultation distributions with natural variance around department baselines.
- Every synthetic record is explicitly stamped with `IsSyntheticDemo = true` in PostgreSQL.
- Production reporting pipelines filter out demo records to preserve real-world clinical integrity.
