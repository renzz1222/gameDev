using System.Collections.Generic;
using UnityEngine;

namespace BawalUmihiMaze
{
    [DisallowMultipleComponent]
    public class MazeMetadata : MonoBehaviour
    {
        [Header("Maze Generation Settings")]
        [Min(6)] public int gridWidth = 24;
        [Min(6)] public int gridHeight = 24;
        [Min(2.5f)] public float cellSize = 3.6f;
        [Min(1.8f)] public float wallHeight = 2.75f;
        public uint seed = 574;
        [Range(0f, 0.85f)] public float straightCorridorBias = 0.48f;

        [Header("Debug Visualization")]
        [Tooltip("Toggle in Scene View to preview the shortest route from Entrance to Exit.")]
        public bool showSolutionPathGizmo = false;
        public bool showDeadEndMarkersGizmo = false;

        [Header("Generated Maze Stats (Read-Only)")]
        public int totalCells;
        public Vector2 footprintMeters;
        public int solutionPathCells;
        public float solutionPathMeters;
        public float totalCorridorMeters;
        public int extensionWallCount;
        public int bawalUmihiDeadEndCount;
        public Vector3 entranceWorldPos;
        public Vector3 exitWorldPos;

        [HideInInspector] public List<Vector3> solutionWaypoints = new List<Vector3>();
        [HideInInspector] public List<Vector3> deadEndPositions = new List<Vector3>();

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.15f, 0.95f, 0.35f, 0.9f);
            Gizmos.DrawWireSphere(entranceWorldPos + Vector3.up * 1.2f, 0.75f);

            Gizmos.color = new Color(1.0f, 0.82f, 0.15f, 0.9f);
            Gizmos.DrawWireSphere(exitWorldPos + Vector3.up * 1.2f, 0.75f);

            if (showSolutionPathGizmo && solutionWaypoints != null && solutionWaypoints.Count > 1)
            {
                Gizmos.color = new Color(0.2f, 1.0f, 0.45f, 0.95f);
                Vector3 lift = Vector3.up * 0.45f;
                for (int i = 0; i < solutionWaypoints.Count - 1; i++)
                {
                    Gizmos.DrawLine(solutionWaypoints[i] + lift, solutionWaypoints[i + 1] + lift);
                }
            }

            if (showDeadEndMarkersGizmo && deadEndPositions != null)
            {
                Gizmos.color = new Color(0.25f, 0.65f, 1.0f, 0.85f);
                for (int i = 0; i < deadEndPositions.Count; i++)
                {
                    Gizmos.DrawWireCube(deadEndPositions[i] + Vector3.up * 1.4f, new Vector3(0.6f, 0.6f, 0.6f));
                }
            }
        }
    }
}
