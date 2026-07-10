from __future__ import annotations

import unittest

import numpy as np

from mechabellum_ml.features import build_features, feature_names


class FeatureTests(unittest.TestCase):
    def test_perspectives_are_exact_opposites(self) -> None:
        snapshot = {
            "round": 4,
            "players": [
                {
                    "reactorCore": 3200,
                    "supply": 100,
                    "previousFightResult": "Win",
                    "units": [
                        {
                            "unitId": 3,
                            "displayLevel": 2,
                            "experience": 120,
                            "x": -40,
                            "y": -150,
                            "equipmentId": 7,
                            "sellSupply": 400,
                        }
                    ],
                    "activeTechnologies": [{"unitId": 3, "technologyIds": [101, 102]}],
                },
                {
                    "reactorCore": 2800,
                    "supply": 50,
                    "previousFightResult": "Lose",
                    "units": [
                        {
                            "unitId": 10,
                            "displayLevel": 1,
                            "experience": 40,
                            "x": 20,
                            "y": 160,
                            "equipmentId": 0,
                            "sellSupply": 200,
                        }
                    ],
                    "activeTechnologies": [],
                },
            ],
        }

        first = build_features(snapshot, 0)
        second = build_features(snapshot, 1)

        self.assertEqual(first.shape, (len(feature_names()),))
        np.testing.assert_allclose(first, -second, atol=1e-12)


if __name__ == "__main__":
    unittest.main()
