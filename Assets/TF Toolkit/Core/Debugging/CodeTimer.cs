using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TF_Toolkit
{
    public class CodeTimer
    {
        public static Dictionary<string, TimeSpan> savedTimes = new Dictionary<string, TimeSpan>();
        public static Dictionary<string, AverageTime> savedAverageTimes = new Dictionary<string, AverageTime>();
        static float framesSinceLastPrint = 0;
        static Dictionary<string, int> skippedCounts = new Dictionary<string, int>();
        const float targetFps = 144;

        Stopwatch sw = Stopwatch.StartNew();

        public void read()
        {
            TimeSpan time = sw.Elapsed;
            UnityEngine.Debug.Log(getTimeAsPercentOfFrameBudget(time));
            sw.Restart();
        }

        public void read(string text)
        {
            TimeSpan time = sw.Elapsed;
            UnityEngine.Debug.Log(text + ": " + getTimeAsPercentOfFrameBudget(time));
            sw.Restart();
        }

        private static TimeSpan getTimeAsPercentOfFrameBudget(TimeSpan time)
        {
            long ticks = time.Ticks;
            return new TimeSpan((long)(ticks * targetFps));
        }

        public void reset()
        {
            sw.Restart();
        }

        public void addTime(string name, int amountToSkip = 0)
        {
            TimeSpan time = sw.Elapsed;

            skippedCounts.TryGetValue(name, out int skippedCount);
            if (skippedCount < amountToSkip)
            {
                skippedCounts[name] = skippedCount + 1;
                return;
            }
            

            if (savedTimes.TryGetValue(name, out TimeSpan savedTime))
            {
                time += savedTime;
                savedTimes.Remove(name);
            }
            savedTimes.Add(name, time);
            sw.Restart();
        }

        public static void printSavedTimes()
        {
            framesSinceLastPrint++;
            bool print = false;
            if (framesSinceLastPrint > 60)
            {
                framesSinceLastPrint = 0;
                print = true;
            }
            foreach (var pair in savedTimes)
            {
                string name = pair.Key;
                TimeSpan time = pair.Value;

                if (savedAverageTimes.TryGetValue(name, out AverageTime averageTime))
                {
                    averageTime.addTime(time);
                }
                else
                {
                    savedAverageTimes[name] = new AverageTime(time);
                }

                //if (print)
                {
                    UnityEngine.Debug.Log(name + " average: " + getTimeAsPercentOfFrameBudget(savedAverageTimes[name].getAverage()) + "frame: " + getTimeAsPercentOfFrameBudget(time));
                }
            }
            savedTimes.Clear();
        }

        public class AverageTime
        {
            public TimeSpan totalTime;
            public float amount;

            public AverageTime(TimeSpan time)
            {
                totalTime = time;
                amount = 1;
            }

            public TimeSpan getAverage()
            {
                long ticks = totalTime.Ticks;
                return new TimeSpan((long)(ticks / amount));
            }

            public void addTime(TimeSpan time)
            {
                totalTime += time;
                amount++;
            }
        }
    }
}