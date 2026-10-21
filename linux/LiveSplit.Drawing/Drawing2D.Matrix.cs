using System.Numerics;

namespace System.Drawing.Drawing2D
{
    /// <summary>GDI+ compatible 3x2 affine matrix (row-vector convention: p' = p * M).</summary>
    public sealed class Matrix : IDisposable
    {
        private float _m11 = 1, _m12, _m21, _m22 = 1, _dx, _dy;

        public Matrix()
        {
        }

        public Matrix(float m11, float m12, float m21, float m22, float dx, float dy)
        {
            _m11 = m11; _m12 = m12; _m21 = m21; _m22 = m22; _dx = dx; _dy = dy;
        }

        public Matrix(Matrix3x2 matrix) : this(matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32)
        {
        }

        /// <summary>Maps rect onto the parallelogram given by three points (upper-left, upper-right, lower-left).</summary>
        public Matrix(RectangleF rect, PointF[] plgpts)
        {
            if (plgpts == null || plgpts.Length != 3)
            {
                throw new ArgumentException("Three points are required.", nameof(plgpts));
            }

            if (rect.Width == 0 || rect.Height == 0)
            {
                throw new ArgumentException("Rectangle must not be empty.", nameof(rect));
            }

            float w = rect.Width, h = rect.Height;
            _m11 = (plgpts[1].X - plgpts[0].X) / w;
            _m12 = (plgpts[1].Y - plgpts[0].Y) / w;
            _m21 = (plgpts[2].X - plgpts[0].X) / h;
            _m22 = (plgpts[2].Y - plgpts[0].Y) / h;
            _dx = plgpts[0].X - (rect.X * _m11) - (rect.Y * _m21);
            _dy = plgpts[0].Y - (rect.X * _m12) - (rect.Y * _m22);
        }

        public Matrix(Rectangle rect, Point[] plgpts)
            : this(rect, plgpts == null ? null : Array.ConvertAll(plgpts, p => (PointF)p))
        {
        }

        public float[] Elements => [_m11, _m12, _m21, _m22, _dx, _dy];

        public Matrix3x2 MatrixElements
        {
            get => new Matrix3x2(_m11, _m12, _m21, _m22, _dx, _dy);
            set { _m11 = value.M11; _m12 = value.M12; _m21 = value.M21; _m22 = value.M22; _dx = value.M31; _dy = value.M32; }
        }

        public float OffsetX => _dx;

        public float OffsetY => _dy;

        public bool IsIdentity => _m11 == 1 && _m12 == 0 && _m21 == 0 && _m22 == 1 && _dx == 0 && _dy == 0;

        public bool IsInvertible => Math.Abs((_m11 * _m22) - (_m12 * _m21)) > 1e-12f;

        public Matrix Clone() => new Matrix(_m11, _m12, _m21, _m22, _dx, _dy);

        public void Dispose()
        {
        }

        public void Reset()
        {
            _m11 = 1; _m12 = 0; _m21 = 0; _m22 = 1; _dx = 0; _dy = 0;
        }

        private void Apply(float m11, float m12, float m21, float m22, float dx, float dy, MatrixOrder order)
        {
            // "op" is applied first when prepending, last when appending.
            if (order == MatrixOrder.Prepend)
            {
                Set(m11, m12, m21, m22, dx, dy, _m11, _m12, _m21, _m22, _dx, _dy);
            }
            else
            {
                Set(_m11, _m12, _m21, _m22, _dx, _dy, m11, m12, m21, m22, dx, dy);
            }
        }

        // this = a * b (a is applied first).
        private void Set(float a11, float a12, float a21, float a22, float adx, float ady,
                         float b11, float b12, float b21, float b22, float bdx, float bdy)
        {
            _m11 = (a11 * b11) + (a12 * b21);
            _m12 = (a11 * b12) + (a12 * b22);
            _m21 = (a21 * b11) + (a22 * b21);
            _m22 = (a21 * b12) + (a22 * b22);
            _dx = (adx * b11) + (ady * b21) + bdx;
            _dy = (adx * b12) + (ady * b22) + bdy;
        }

        public void Multiply(Matrix matrix) => Multiply(matrix, MatrixOrder.Prepend);

        public void Multiply(Matrix matrix, MatrixOrder order)
        {
            if (matrix == null)
            {
                throw new ArgumentNullException(nameof(matrix));
            }

            Apply(matrix._m11, matrix._m12, matrix._m21, matrix._m22, matrix._dx, matrix._dy, order);
        }

        public void Translate(float offsetX, float offsetY) => Translate(offsetX, offsetY, MatrixOrder.Prepend);

        public void Translate(float offsetX, float offsetY, MatrixOrder order) => Apply(1, 0, 0, 1, offsetX, offsetY, order);

        public void Scale(float scaleX, float scaleY) => Scale(scaleX, scaleY, MatrixOrder.Prepend);

        public void Scale(float scaleX, float scaleY, MatrixOrder order) => Apply(scaleX, 0, 0, scaleY, 0, 0, order);

        public void Rotate(float angle) => Rotate(angle, MatrixOrder.Prepend);

        public void Rotate(float angle, MatrixOrder order)
        {
            double rad = angle * Math.PI / 180.0;
            float cos = (float)Math.Cos(rad), sin = (float)Math.Sin(rad);
            if (angle % 90f == 0)
            {
                // Avoid floating point noise for the common right angles.
                switch (((int)(angle % 360f) + 360) % 360)
                {
                    case 0: cos = 1; sin = 0; break;
                    case 90: cos = 0; sin = 1; break;
                    case 180: cos = -1; sin = 0; break;
                    case 270: cos = 0; sin = -1; break;
                }
            }

            Apply(cos, sin, -sin, cos, 0, 0, order);
        }

        public void RotateAt(float angle, PointF point) => RotateAt(angle, point, MatrixOrder.Prepend);

        public void RotateAt(float angle, PointF point, MatrixOrder order)
        {
            if (order == MatrixOrder.Prepend)
            {
                Translate(point.X, point.Y, MatrixOrder.Prepend);
                Rotate(angle, MatrixOrder.Prepend);
                Translate(-point.X, -point.Y, MatrixOrder.Prepend);
            }
            else
            {
                Translate(-point.X, -point.Y, MatrixOrder.Append);
                Rotate(angle, MatrixOrder.Append);
                Translate(point.X, point.Y, MatrixOrder.Append);
            }
        }

        public void Shear(float shearX, float shearY) => Shear(shearX, shearY, MatrixOrder.Prepend);

        public void Shear(float shearX, float shearY, MatrixOrder order) => Apply(1, shearY, shearX, 1, 0, 0, order);

        /// <summary>Inverts the matrix. A singular matrix is left unchanged (GDI+ would throw).</summary>
        public void Invert()
        {
            float det = (_m11 * _m22) - (_m12 * _m21);
            if (Math.Abs(det) <= 1e-12f)
            {
                return;
            }

            float i11 = _m22 / det, i12 = -_m12 / det, i21 = -_m21 / det, i22 = _m11 / det;
            float idx = -((_dx * i11) + (_dy * i21));
            float idy = -((_dx * i12) + (_dy * i22));
            _m11 = i11; _m12 = i12; _m21 = i21; _m22 = i22; _dx = idx; _dy = idy;
        }

        public void TransformPoints(PointF[] pts)
        {
            if (pts == null)
            {
                throw new ArgumentNullException(nameof(pts));
            }

            for (int i = 0; i < pts.Length; i++)
            {
                float x = pts[i].X, y = pts[i].Y;
                pts[i] = new PointF((x * _m11) + (y * _m21) + _dx, (x * _m12) + (y * _m22) + _dy);
            }
        }

        public void TransformPoints(Point[] pts)
        {
            if (pts == null)
            {
                throw new ArgumentNullException(nameof(pts));
            }

            for (int i = 0; i < pts.Length; i++)
            {
                float x = pts[i].X, y = pts[i].Y;
                pts[i] = new Point(
                    (int)Math.Round((x * _m11) + (y * _m21) + _dx, MidpointRounding.AwayFromZero),
                    (int)Math.Round((x * _m12) + (y * _m22) + _dy, MidpointRounding.AwayFromZero));
            }
        }

        public void TransformVectors(PointF[] pts)
        {
            if (pts == null)
            {
                throw new ArgumentNullException(nameof(pts));
            }

            for (int i = 0; i < pts.Length; i++)
            {
                float x = pts[i].X, y = pts[i].Y;
                pts[i] = new PointF((x * _m11) + (y * _m21), (x * _m12) + (y * _m22));
            }
        }

        public void TransformVectors(Point[] pts)
        {
            if (pts == null)
            {
                throw new ArgumentNullException(nameof(pts));
            }

            for (int i = 0; i < pts.Length; i++)
            {
                float x = pts[i].X, y = pts[i].Y;
                pts[i] = new Point(
                    (int)Math.Round((x * _m11) + (y * _m21), MidpointRounding.AwayFromZero),
                    (int)Math.Round((x * _m12) + (y * _m22), MidpointRounding.AwayFromZero));
            }
        }

        public void VectorTransformPoints(Point[] pts) => TransformVectors(pts);

        internal PointF Transform(PointF p)
        {
            return new PointF((p.X * _m11) + (p.Y * _m21) + _dx, (p.X * _m12) + (p.Y * _m22) + _dy);
        }

        public bool Equals(Matrix other)
        {
            return other != null && _m11 == other._m11 && _m12 == other._m12 && _m21 == other._m21
                && _m22 == other._m22 && _dx == other._dx && _dy == other._dy;
        }

        public override bool Equals(object obj) => obj is Matrix m && Equals(m);

        public override int GetHashCode() => HashCode.Combine(_m11, _m12, _m21, _m22, _dx, _dy);

        public override string ToString() => $"[{_m11}, {_m12}, {_m21}, {_m22}, {_dx}, {_dy}]";
    }
}
