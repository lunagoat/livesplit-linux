using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace System.Drawing
{
    /// <summary>
    /// Same contract as System.Drawing.ImageAnimator: <see cref="Animate"/> starts tracking an image,
    /// the handler fires when a new frame is due, and <see cref="UpdateFrames()"/> applies the due frame.
    /// </summary>
    public static class ImageAnimator
    {
        private sealed class State
        {
            public Image Image;
            public List<EventHandler> Handlers = new();
            public long NextFrameAtMs;
            public int NextFrame;
            public bool Pending;
        }

        private static readonly object Lock = new();
        private static readonly List<State> States = new();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static Timer _timer;

        public static bool CanAnimate(Image image) => image != null && image.GetFrameCount(Imaging.FrameDimension.Time) > 1;

        public static void Animate(Image image, EventHandler onFrameChangedHandler)
        {
            if (!CanAnimate(image))
            {
                return;
            }

            lock (Lock)
            {
                State s = States.Find(x => ReferenceEquals(x.Image, image));
                if (s == null)
                {
                    s = new State { Image = image, NextFrame = 0 };
                    s.NextFrameAtMs = Clock.ElapsedMilliseconds + image.FrameDelayMs(image.CurrentFrameIndex);
                    s.NextFrame = (image.CurrentFrameIndex + 1) % image.GetFrameCount(Imaging.FrameDimension.Time);
                    States.Add(s);
                }

                if (onFrameChangedHandler != null)
                {
                    s.Handlers.Add(onFrameChangedHandler);
                }

                _timer ??= new Timer(Tick, null, 15, 15);
            }
        }

        public static void StopAnimate(Image image, EventHandler onFrameChangedHandler)
        {
            lock (Lock)
            {
                State s = States.Find(x => ReferenceEquals(x.Image, image));
                if (s == null)
                {
                    return;
                }

                s.Handlers.Remove(onFrameChangedHandler);
                if (s.Handlers.Count == 0)
                {
                    States.Remove(s);
                }
            }
        }

        internal static void StopAnimate(Image image)
        {
            lock (Lock)
            {
                States.RemoveAll(x => ReferenceEquals(x.Image, image));
            }
        }

        public static void UpdateFrames()
        {
            lock (Lock)
            {
                foreach (State s in States)
                {
                    Apply(s);
                }
            }
        }

        public static void UpdateFrames(Image image)
        {
            if (image == null)
            {
                return;
            }

            lock (Lock)
            {
                State s = States.Find(x => ReferenceEquals(x.Image, image));
                if (s != null)
                {
                    Apply(s);
                }
            }
        }

        private static void Apply(State s)
        {
            if (s.Pending && !s.Image.IsDisposed)
            {
                s.Image.SetCurrentFrameInternal(s.NextFrame == 0 ? s.Image.GetFrameCount(Imaging.FrameDimension.Time) - 1 : s.NextFrame - 1);
                s.Pending = false;
            }
        }

        private static void Tick(object state)
        {
            var toNotify = new List<(State, EventHandler[])>();
            lock (Lock)
            {
                long now = Clock.ElapsedMilliseconds;
                foreach (State s in States)
                {
                    if (s.Image.IsDisposed || s.Pending || now < s.NextFrameAtMs)
                    {
                        continue;
                    }

                    int count = s.Image.GetFrameCount(Imaging.FrameDimension.Time);
                    s.Pending = true;
                    int showing = s.NextFrame;
                    s.NextFrame = (showing + 1) % count;
                    s.NextFrameAtMs = now + Math.Max(10, s.Image.FrameDelayMs(showing));
                    toNotify.Add((s, s.Handlers.ToArray()));
                }
            }

            foreach ((State s, EventHandler[] handlers) in toNotify)
            {
                foreach (EventHandler h in handlers)
                {
                    try
                    {
                        h(s.Image, EventArgs.Empty);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
