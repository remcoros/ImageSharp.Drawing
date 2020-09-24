// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Drawing.Shapes.Scan
{
    internal partial class ScanEdgeCollection
    {
        private enum EdgeCategory
        {
            Up = 0, // Non-horizontal
            Down, // Non-horizontal
            Left, // Horizontal
            Right, // Horizontal
        }

        // A pair of EdgeCategories at a given vertex defined as (fromEdge.EdgeCategory, toEdge.EdgeCategory)
        private enum VertexCategory
        {
            UpUp = 0,
            UpDown,
            UpLeft,
            UpRight,

            DownUp,
            DownDown,
            DownLeft,
            DownRight,

            LeftUp,
            LeftDown,
            LeftLeft,
            LeftRight,

            RightUp,
            RightDown,
            RightLeft,
            RightRight,
        }

        private struct EdgeData
        {
            public EdgeCategory EdgeCategory;

            private PointF start;
            private PointF end;
            private int emitStart;
            private int emitEnd;

            public EdgeData(PointF start, PointF end, in TolerantComparer comparer)
            {
                this.start = start;
                this.end = end;
                if (comparer.AreEqual(this.start.Y, this.end.Y))
                {
                    this.EdgeCategory = this.start.X < this.end.X ? EdgeCategory.Right : EdgeCategory.Left;
                }
                else
                {
                    this.EdgeCategory = this.start.Y < this.end.Y ? EdgeCategory.Down : EdgeCategory.Up;
                }

                this.emitStart = 0;
                this.emitEnd = 0;
            }

            public void EmitScanEdge(Span<ScanEdge> edges, ref int edgeCounter)
            {
                if (this.EdgeCategory == EdgeCategory.Left || this.EdgeCategory == EdgeCategory.Right)
                {
                    return;
                }

                edges[edgeCounter++] = this.ToScanEdge();
            }

            public static void ApplyVertexCategory(
                VertexCategory vertexCategory,
                ref EdgeData fromEdge,
                ref EdgeData toEdge)
            {
                switch (vertexCategory)
                {
                    case VertexCategory.UpUp:
                        // 0, 1
                        toEdge.emitStart = 1;
                        break;
                    case VertexCategory.UpDown:
                        // 0, 0
                        break;
                    case VertexCategory.UpLeft:
                        // 2, 0
                        fromEdge.emitEnd = 2;
                        break;
                    case VertexCategory.UpRight:
                        // 1, 0
                        fromEdge.emitEnd = 1;
                        break;
                    case VertexCategory.DownUp:
                        // 0, 0
                        break;
                    case VertexCategory.DownDown:
                        // 0, 1
                        toEdge.emitStart = 1;
                        break;
                    case VertexCategory.DownLeft:
                        // 1, 0
                        fromEdge.emitEnd = 1;
                        break;
                    case VertexCategory.DownRight:
                        // 2, 0
                        fromEdge.emitEnd = 2;
                        break;
                    case VertexCategory.LeftUp:
                        // 0, 1
                        toEdge.emitStart = 1;
                        break;
                    case VertexCategory.LeftDown:
                        // 0, 2
                        toEdge.emitStart = 2;
                        break;
                    case VertexCategory.LeftLeft:
                        // INVALID
                        ThrowInvalidRing("Invalid ring: repeated horizontal edges (<- <-)");
                        break;
                    case VertexCategory.LeftRight:
                        // INVALID
                        ThrowInvalidRing("Invalid ring: repeated horizontal edges (<- ->)");
                        break;
                    case VertexCategory.RightUp:
                        // 0, 2
                        toEdge.emitStart = 2;
                        break;
                    case VertexCategory.RightDown:
                        // 0, 1
                        toEdge.emitStart = 1;
                        break;
                    case VertexCategory.RightLeft:
                        // INVALID
                        ThrowInvalidRing("Invalid ring: repeated horizontal edges (-> <-)");
                        break;
                    case VertexCategory.RightRight:
                        // INVALID
                        ThrowInvalidRing("Invalid ring: repeated horizontal edges (-> ->)");
                        break;
                }
            }

            private ScanEdge ToScanEdge()
            {
                int up = this.EdgeCategory == EdgeCategory.Up ? 1 : 0;
                if (up == 1)
                {
                    Swap(ref this.start, ref this.end);
                    Swap(ref this.emitStart, ref this.emitEnd);
                }

                int flags = up | (this.emitStart << 1) | (this.emitEnd << 3);
                return new ScanEdge(ref this.start, ref this.end, flags);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void Swap<T>(ref T left, ref T right)
            {
                T tmp = left;
                left = right;
                right = tmp;
            }
        }

        private ref struct RingWalker
        {
            private readonly Span<ScanEdge> output;
            public int EdgeCounter;

            public EdgeData PreviousEdge;
            public EdgeData CurrentEdge;
            public EdgeData NextEdge;

            public RingWalker(Span<ScanEdge> output)
            {
                this.output = output;
                this.EdgeCounter = 0;
                this.PreviousEdge = default;
                this.CurrentEdge = default;
                this.NextEdge = default;
            }

            public void Move(bool emitPreviousEdge)
            {
                VertexCategory startVertexCategory =
                    CreateVertexCategory(this.PreviousEdge.EdgeCategory, this.CurrentEdge.EdgeCategory);
                VertexCategory endVertexCategory =
                    CreateVertexCategory(this.CurrentEdge.EdgeCategory, this.NextEdge.EdgeCategory);

                EdgeData.ApplyVertexCategory(startVertexCategory, ref this.PreviousEdge, ref this.CurrentEdge);
                EdgeData.ApplyVertexCategory(endVertexCategory, ref this.CurrentEdge, ref this.NextEdge);

                if (emitPreviousEdge)
                {
                    this.PreviousEdge.EmitScanEdge(this.output, ref this.EdgeCounter);
                }

                this.PreviousEdge = this.CurrentEdge;
                this.CurrentEdge = this.NextEdge;
            }
        }

        private static ScanEdgeCollection Create(TessellatedMultipolygon multipolygon, MemoryAllocator allocator, in TolerantComparer comparer)
        {
            // We allocate more than we need, since we don't know how many horizontal edges do we have:
            IMemoryOwner<ScanEdge> buffer = allocator.Allocate<ScanEdge>(multipolygon.TotalVertexCount);

            RingWalker walker = new RingWalker(buffer.Memory.Span);

            foreach (TessellatedMultipolygon.Ring ring in multipolygon)
            {
                if (ring.VertexCount < 3)
                {
                    ThrowInvalidRing("ScanEdgeCollection.Create Encountered a ring with VertexCount < 3!");
                }

                var vertices = ring.Vertices;

                walker.PreviousEdge = new EdgeData(vertices[vertices.Length - 2], vertices[vertices.Length - 1], comparer); // Last edge
                walker.CurrentEdge = new EdgeData(vertices[0], vertices[1], comparer); // First edge
                walker.NextEdge = new EdgeData(vertices[1], vertices[2], comparer); // Second edge
                walker.Move(false);

                for (int i = 1; i < vertices.Length - 2; i++)
                {
                    walker.NextEdge = new EdgeData(vertices[i + 1], vertices[i + 2], comparer);
                    walker.Move(true);
                }

                walker.NextEdge = new EdgeData(vertices[0], vertices[1], comparer); // First edge
                walker.Move(true); // Emit edge before last edge

                walker.NextEdge = new EdgeData(vertices[1], vertices[2], comparer); // Second edge
                walker.Move(true); // Emit last edge
            }

            return new ScanEdgeCollection(buffer, walker.EdgeCounter);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VertexCategory CreateVertexCategory(EdgeCategory previousCategory, EdgeCategory currentCategory)
        {
            var value = (VertexCategory)(((int)previousCategory << 2) | (int)currentCategory);
            VerifyVertexCategory(value);
            return value;
        }

        [Conditional("DEBUG")]
        private static void VerifyVertexCategory(VertexCategory vertexCategory)
        {
            int value = (int) vertexCategory;
            if (value < 0 || value >= 16)
            {
                throw new Exception("EdgeCategoryPair value shall be: 0 <= value < 16");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowInvalidRing(string message)
        {
            throw new InvalidOperationException(message);
        }
    }
}