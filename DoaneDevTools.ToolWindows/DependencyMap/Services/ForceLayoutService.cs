using System;
using System.Collections.Generic;
using System.Linq;
using DoaneDevTools.ToolWindows.DependencyMap.Models;

namespace DoaneDevTools.ToolWindows.DependencyMap.Services
{
    /// <summary>
    /// Implements the Fruchterman-Reingold force-directed graph layout algorithm.
    ///
    /// The algorithm simulates two forces on each node pair:
    ///   • Repulsion  — every pair of nodes repels each other (inverse-square law).
    ///   • Attraction — every edge between two nodes pulls them together (spring).
    ///
    /// A temperature variable decreases each iteration (simulated annealing /
    /// "cooling") so that the layout converges rather than oscillating.
    /// </summary>
    public class ForceLayoutService
    {
        // -----------------------------------------------------------------------
        // Parameters (can be tuned via constructor or properties)
        // -----------------------------------------------------------------------

        /// <summary>Canvas width used to initialise node positions and clamp output.</summary>
        public double CanvasWidth { get; set; } = 1200;

        /// <summary>Canvas height used to initialise node positions and clamp output.</summary>
        public double CanvasHeight { get; set; } = 800;

        /// <summary>Number of simulation iterations to run.</summary>
        public int Iterations { get; set; } = 200;

        /// <summary>
        /// Repulsion constant (k²). Larger values push nodes further apart.
        /// Defaults to an area-proportional value computed in <see cref="Run"/>.
        /// Set to a positive value to override.
        /// </summary>
        public double RepulsionConstant { get; set; } = 0;

        /// <summary>
        /// Attraction constant multiplier. Values &lt; 1 produce looser clusters;
        /// values &gt; 1 pull connected nodes together more aggressively.
        /// </summary>
        public double AttractionConstant { get; set; } = 1.0;

        /// <summary>
        /// Cooling factor applied to the temperature each iteration.
        /// Must be in (0, 1). Smaller values converge faster but may
        /// produce less optimal layouts.
        /// </summary>
        public double CoolingFactor { get; set; } = 0.95;

        // -----------------------------------------------------------------------
        // Main entry point
        // -----------------------------------------------------------------------

        /// <summary>
        /// Runs the Fruchterman-Reingold layout algorithm on <paramref name="nodes"/>
        /// using the adjacency information in <paramref name="edges"/>.
        /// Modifies <see cref="GraphNode.X"/> and <see cref="GraphNode.Y"/> in place.
        /// </summary>
        public void Run(List<GraphNode> nodes, List<GraphEdge> edges)
        {
            if (nodes == null || nodes.Count == 0) return;

            var n = nodes.Count;

            // Area of the canvas.
            double area = CanvasWidth * CanvasHeight;

            // Ideal edge length k such that k² = area / n.
            double k = Math.Sqrt(area / Math.Max(n, 1));
            double repulsion = RepulsionConstant > 0 ? RepulsionConstant : k * k;

            // Build index for fast ID→node lookup.
            var index = nodes.ToDictionary(nd => nd.Id, nd => nd);

            // Build adjacency set for O(1) edge existence check.
            var adjacency = new HashSet<(string, string)>(
                edges.Select(e => (e.SourceId, e.TargetId)));

            // Initialise positions on a grid / circle if all are at (0,0).
            InitialisePositions(nodes);

            // Displacement accumulator arrays (parallel to nodes list).
            var dx = new double[n];
            var dy = new double[n];

            // Initial temperature = 10 % of the canvas diagonal.
            double temperature = Math.Sqrt(area) * 0.1;

            // ── Main simulation loop ─────────────────────────────────────────
            for (int iter = 0; iter < Iterations; iter++)
            {
                Array.Clear(dx, 0, n);
                Array.Clear(dy, 0, n);

                // ── Repulsive forces (all pairs) ─────────────────────────────
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        double deltaX = nodes[i].X - nodes[j].X;
                        double deltaY = nodes[i].Y - nodes[j].Y;
                        double distance = Math.Max(Math.Sqrt(deltaX * deltaX + deltaY * deltaY), 0.01);

                        // fr(d) = k² / d
                        double force = repulsion / distance;
                        double forceX = (deltaX / distance) * force;
                        double forceY = (deltaY / distance) * force;

                        dx[i] += forceX;
                        dy[i] += forceY;
                        dx[j] -= forceX;
                        dy[j] -= forceY;
                    }
                }

                // ── Attractive forces (edges only) ───────────────────────────
                foreach (var edge in edges)
                {
                    if (!index.TryGetValue(edge.SourceId, out var src)) continue;
                    if (!index.TryGetValue(edge.TargetId, out var tgt)) continue;

                    int si = nodes.IndexOf(src);
                    int ti = nodes.IndexOf(tgt);
                    if (si < 0 || ti < 0) continue;

                    double deltaX  = tgt.X - src.X;
                    double deltaY  = tgt.Y - src.Y;
                    double distance = Math.Max(Math.Sqrt(deltaX * deltaX + deltaY * deltaY), 0.01);

                    // fa(d) = d² / k
                    double force  = AttractionConstant * (distance * distance) / k;
                    double forceX = (deltaX / distance) * force;
                    double forceY = (deltaY / distance) * force;

                    dx[si] += forceX;
                    dy[si] += forceY;
                    dx[ti] -= forceX;
                    dy[ti] -= forceY;
                }

                // ── Apply displacements, clamped to temperature ──────────────
                for (int i = 0; i < n; i++)
                {
                    double dispLen = Math.Max(Math.Sqrt(dx[i] * dx[i] + dy[i] * dy[i]), 0.01);
                    double clamped = Math.Min(dispLen, temperature);

                    nodes[i].X += (dx[i] / dispLen) * clamped;
                    nodes[i].Y += (dy[i] / dispLen) * clamped;

                    // Keep node within canvas bounds, accounting for node size.
                    double halfW = nodes[i].Width  / 2;
                    double halfH = nodes[i].Height / 2;

                    nodes[i].X = Clamp(nodes[i].X, halfW, CanvasWidth  - halfW);
                    nodes[i].Y = Clamp(nodes[i].Y, halfH, CanvasHeight - halfH);
                }

                // ── Cool down ────────────────────────────────────────────────
                temperature *= CoolingFactor;

                // Early exit if temperature has become negligible.
                if (temperature < 0.001) break;
            }
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Places nodes on a regular grid if they are all at the origin.
        /// Nodes already positioned (e.g. from a previous layout run) are kept.
        /// </summary>
        private void InitialisePositions(List<GraphNode> nodes)
        {
            bool allAtOrigin = nodes.All(n => n.X == 0 && n.Y == 0);
            if (!allAtOrigin) return;

            int count  = nodes.Count;
            int cols   = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count)));

            double cellW = CanvasWidth  / (cols + 1);
            double cellH = CanvasHeight / (Math.Ceiling((double)count / cols) + 1);

            // Add a small jitter to break symmetry (needed for FR to work well).
            var rng = new Random(42);

            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;

                nodes[i].X = (col + 1) * cellW + (rng.NextDouble() - 0.5) * cellW * 0.3;
                nodes[i].Y = (row + 1) * cellH + (rng.NextDouble() - 0.5) * cellH * 0.3;
            }
        }

        private static double Clamp(double value, double min, double max) =>
            Math.Max(min, Math.Min(max, value));
    }
}
