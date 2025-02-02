﻿/*****************************************************************************
 * RasterPropMonitor
 * =================
 * Plugin for Kerbal Space Program
 *
 *  by Mihara (Eugene Medvedev), MOARdV, and other contributors
 * 
 * RasterPropMonitor is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, revision
 * date 29 June 2007, or (at your option) any later version.
 * 
 * RasterPropMonitor is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License
 * for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with RasterPropMonitor.  If not, see <http://www.gnu.org/licenses/>.
 ****************************************************************************/
using System;

namespace JSI
{
    // This entire class is imported wholesale from MechJeb.
    public static class OrbitExtensions
    {
        // These "Swapped" functions translate preexisting Orbit class functions into world
        // space. For some reason, Orbit class functions seem to use a coordinate system
        // in which the Y and Z coordinates are swapped.
        //
        public static Vector3d SwappedOrbitalVelocityAtUT(this Orbit o, double UT)
        {
            return o.getOrbitalVelocityAtUT(UT).xzy;
        }
        //position relative to the primary
        public static Vector3d SwappedRelativePositionAtUT(this Orbit o, double UT)
        {
            return o.getRelativePositionAtUT(UT).xzy;
        }
        //position in world space
        public static Vector3d SwappedAbsolutePositionAtUT(this Orbit o, double UT)
        {
            return o.referenceBody.position + o.SwappedRelativePositionAtUT(UT);
        }
        //normalized vector perpendicular to the orbital plane
        //convention: as you look down along the orbit normal, the satellite revolves counterclockwise
        public static Vector3d SwappedOrbitNormal(this Orbit o)
        {
            return -(o.GetOrbitNormal().xzy).normalized;
        }
        //normalized vector along the orbital velocity
        public static Vector3d Prograde(this Orbit o, double UT)
        {
            return o.SwappedOrbitalVelocityAtUT(UT).normalized;
        }
        //normalized vector pointing radially outward from the planet
        public static Vector3d Up(this Orbit o, double UT)
        {
            return o.SwappedRelativePositionAtUT(UT).normalized;
        }
        //normalized vector pointing radially outward and perpendicular to prograde
        public static Vector3d RadialPlus(this Orbit o, double UT)
        {
            return Vector3d.Exclude(o.Prograde(UT), o.Up(UT)).normalized;
        }
        //another name for the orbit normal; this form makes it look like the other directions
        // Analysis disable once UnusedParameter
        public static Vector3d NormalPlus(this Orbit o, double UT)
        {
            return o.SwappedOrbitNormal();
        }
        //normalized vector parallel to the planet's surface, and pointing in the same general direction as the orbital velocity
        //(parallel to an ideally spherical planet's surface, anyway)
        public static Vector3d Horizontal(this Orbit o, double UT)
        {
            return Vector3d.Exclude(o.Up(UT), o.Prograde(UT)).normalized;
        }
        //normalized vector parallel to the planet's surface and pointing in the northward direction
        public static Vector3d North(this Orbit o, double UT)
        {
            return Vector3d.Exclude(o.Up(UT), (o.referenceBody.transform.up * (float)o.referenceBody.Radius) - o.SwappedRelativePositionAtUT(UT)).normalized;
        }
        //normalized vector parallel to the planet's surface and pointing in the eastward direction
        public static Vector3d East(this Orbit o, double UT)
        {
            return Vector3d.Cross(o.Up(UT), o.North(UT)); //I think this is the opposite of what it should be, but it gives the right answer
        }
        //distance from the center of the planet
        public static double Radius(this Orbit o, double UT)
        {
            return o.SwappedRelativePositionAtUT(UT).magnitude;
        }
        //returns a new Orbit object that represents the result of applying a given dV to o at UT
        public static Orbit PerturbedOrbit(this Orbit o, double UT, Vector3d dV)
        {
            //should these in fact be swapped?
            return JUtil.OrbitFromStateVectors(o.SwappedAbsolutePositionAtUT(UT), o.SwappedOrbitalVelocityAtUT(UT) + dV, o.referenceBody, UT);
        }
        //mean motion is rate of increase of the mean anomaly
        public static double MeanMotion(this Orbit o)
        {
            return Math.Sqrt(o.referenceBody.gravParameter / Math.Abs(Math.Pow(o.semiMajorAxis, 3)));
        }
        //distance between two orbiting objects at a given time
        public static double Separation(this Orbit a, Orbit b, double UT)
        {
            return (a.SwappedAbsolutePositionAtUT(UT) - b.SwappedAbsolutePositionAtUT(UT)).magnitude;
        }
        //Time during a's next orbit at which object a comes nearest to object b.
        //If a is hyperbolic, the examined interval is the next 100 units of mean anomaly.
        //This is quite a large segment of the hyperbolic arc. However, for extremely high
        //hyperbolic eccentricity it may not find the actual closest approach.
        public static double NextClosestApproachTime(this Orbit a, Orbit b, double UT)
        {
            double closestApproachTime = UT;
            double closestApproachDistance = Double.MaxValue;
            double minTime = UT;
            double interval = a.period;
            if (a.eccentricity > 1)
            {
                interval = 100 / a.MeanMotion(); //this should be an interval of time that covers a large chunk of the hyperbolic arc
            }
            double maxTime = UT + interval;
            const int numDivisions = 20;

            for (int iter = 0; iter < 8; iter++)
            {
                double dt = (maxTime - minTime) / numDivisions;
                for (int i = 0; i < numDivisions; i++)
                {
                    double t = minTime + i * dt;
                    double distance = a.Separation(b, t);
                    if (distance < closestApproachDistance)
                    {
                        closestApproachDistance = distance;
                        closestApproachTime = t;
                    }
                }
                minTime = closestApproachTime - dt.Clamp(UT, UT + interval);
                maxTime = closestApproachTime + dt.Clamp(UT, UT + interval);
            }

            return closestApproachTime;
        }
        //Distance between a and b at the closest approach found by NextClosestApproachTime
        public static double NextClosestApproachDistance(this Orbit a, Orbit b, double UT)
        {
            return a.Separation(b, a.NextClosestApproachTime(b, UT));
        }
        //The mean anomaly of the orbit.
        //For elliptical orbits, the value return is always between 0 and 2pi
        //For hyperbolic orbits, the value can be any number.
        public static double MeanAnomalyAtUT(this Orbit o, double UT)
        {
            double ret = o.meanAnomalyAtEpoch + o.MeanMotion() * (UT - o.epoch);
            if (o.eccentricity < 1)
                ret = JUtil.ClampRadiansTwoPi(ret);
            return ret;
        }
        //The next time at which the orbiting object will reach the given mean anomaly.
        //For elliptical orbits, this will be a time between UT and UT + o.period
        //For hyperbolic orbits, this can be any time, including a time in the past, if
        //the given mean anomaly occurred in the past
        public static double UTAtMeanAnomaly(this Orbit o, double meanAnomaly, double UT)
        {
            double currentMeanAnomaly = o.MeanAnomalyAtUT(UT);
            double meanDifference = meanAnomaly - currentMeanAnomaly;
            if (o.eccentricity < 1)
                meanDifference = JUtil.ClampRadiansTwoPi(meanDifference);
            return UT + meanDifference / o.MeanMotion();
        }
        //The next time at which the orbiting object will be at periapsis.
        //For elliptical orbits, this will be between UT and UT + o.period.
        //For hyperbolic orbits, this can be any time, including a time in the past,
        //if the periapsis is in the past.
        public static double NextPeriapsisTime(this Orbit o, double UT)
        {
            if (o.eccentricity < 1)
            {
                return o.TimeOfTrueAnomaly(0, UT);
            }
            return UT - o.MeanAnomalyAtUT(UT) / o.MeanMotion();
        }
        //Returns the next time at which the orbiting object will be at apoapsis.
        //For elliptical orbits, this is a time between UT and UT + period.
        //For hyperbolic orbits, this throws an ArgumentException.
        public static double NextApoapsisTime(this Orbit o, double UT)
        {
            if (o.eccentricity < 1)
            {
                return o.TimeOfTrueAnomaly(180, UT);
            }
            throw new ArgumentException("OrbitExtensions.NextApoapsisTime cannot be called on hyperbolic orbits");
        }
        //Gives the true anomaly (in a's orbit) at which a crosses its ascending node
        //with b's orbit.
        //The returned value is always between 0 and 360.
        public static double AscendingNodeTrueAnomaly(this Orbit a, Orbit b)
        {
            Vector3d vectorToAN = Vector3d.Cross(a.SwappedOrbitNormal(), b.SwappedOrbitNormal());
            return a.TrueAnomalyFromVector(vectorToAN);
        }
        //Gives the true anomaly (in a's orbit) at which a crosses its descending node
        //with b's orbit.
        //The returned value is always between 0 and 360.
        public static double DescendingNodeTrueAnomaly(this Orbit a, Orbit b)
        {
            return JUtil.ClampDegrees360(a.AscendingNodeTrueAnomaly(b) + 180);
        }
        //Gives the true anomaly at which o crosses the equator going northwards, if o is east-moving,
        //or southwards, if o is west-moving.
        //The returned value is always between 0 and 360.
        public static double AscendingNodeEquatorialTrueAnomaly(this Orbit o)
        {
            Vector3d vectorToAN = Vector3d.Cross(o.referenceBody.transform.up, o.SwappedOrbitNormal());
            return o.TrueAnomalyFromVector(vectorToAN);
        }
        //Gives the true anomaly at which o crosses the equator going southwards, if o is east-moving,
        //or northwards, if o is west-moving.
        //The returned value is always between 0 and 360.
        public static double DescendingNodeEquatorialTrueAnomaly(this Orbit o)
        {
            return JUtil.ClampDegrees360(o.AscendingNodeEquatorialTrueAnomaly() + 180);
        }
        //For hyperbolic orbits, the true anomaly only takes on values in the range
        // -M < true anomaly < +M for some M. This function computes M.
        public static double MaximumTrueAnomaly(this Orbit o)
        {
            if (o.eccentricity < 1)
                return 180;
            return 180 / Math.PI * Math.Acos(-1 / o.eccentricity);
        }
        //Returns whether a has an ascending node with b. This can be false
        //if a is hyperbolic and the would-be ascending node is within the opening
        //angle of the hyperbola.
        public static bool AscendingNodeExists(this Orbit a, Orbit b)
        {
            return Math.Abs(JUtil.ClampDegrees180(a.AscendingNodeTrueAnomaly(b))) <= a.MaximumTrueAnomaly();
        }
        //Returns whether a has a descending node with b. This can be false
        //if a is hyperbolic and the would-be descending node is within the opening
        //angle of the hyperbola.
        public static bool DescendingNodeExists(this Orbit a, Orbit b)
        {
            return Math.Abs(JUtil.ClampDegrees180(a.DescendingNodeTrueAnomaly(b))) <= a.MaximumTrueAnomaly();
        }
        //Returns whether o has an ascending node with the equator. This can be false
        //if o is hyperbolic and the would-be ascending node is within the opening
        //angle of the hyperbola.
        public static bool AscendingNodeEquatorialExists(this Orbit o)
        {
            return Math.Abs(JUtil.ClampDegrees180(o.AscendingNodeEquatorialTrueAnomaly())) <= o.MaximumTrueAnomaly();
        }
        //Returns whether o has a descending node with the equator. This can be false
        //if o is hyperbolic and the would-be descending node is within the opening
        //angle of the hyperbola.
        public static bool DescendingNodeEquatorialExists(this Orbit o)
        {
            return Math.Abs(JUtil.ClampDegrees180(o.DescendingNodeEquatorialTrueAnomaly())) <= o.MaximumTrueAnomaly();
        }
        //Converts a direction, specified by a Vector3d, into a true anomaly.
        //The vector is projected into the orbital plane and then the true anomaly is
        //computed as the angle this vector makes with the vector pointing to the periapsis.
        //The returned value is always between 0 and 360.
        public static double TrueAnomalyFromVector(this Orbit o, Vector3d vec)
        {
            Vector3d projected = Vector3d.Exclude(o.SwappedOrbitNormal(), vec);
            Vector3d vectorToPe = o.GetEccVector().xzy;
            double angleFromPe = Math.Abs(Vector3d.Angle(vectorToPe, projected));

            //If the vector points to the infalling part of the orbit then we need to do 360 minus the
            //angle from Pe to get the true anomaly. Test this by taking the the cross product of the
            //orbit normal and vector to the periapsis. This gives a vector that points to center of the 
            //outgoing side of the orbit. If vectorToAN is more than 90 degrees from this vector, it occurs
            //during the infalling part of the orbit.
            if (Math.Abs(Vector3d.Angle(projected, Vector3d.Cross(o.SwappedOrbitNormal(), vectorToPe))) < 90)
            {
                return angleFromPe;
            }
            return 360 - angleFromPe;
        }
        //Originally by Zool, revised by The_Duck
        //Converts a true anomaly into an eccentric anomaly.
        //For elliptical orbits this returns a value between 0 and 2pi
        //For hyperbolic orbits the returned value can be any number.
        //NOTE: For a hyperbolic orbit, if a true anomaly is requested that does not exist (a true anomaly
        //past the true anomaly of the asymptote) then an ArgumentException is thrown
        public static double GetEccentricAnomalyAtTrueAnomaly(this Orbit o, double trueAnomaly)
        {
            double e = o.eccentricity;
            trueAnomaly = JUtil.ClampDegrees360(trueAnomaly);
            trueAnomaly = trueAnomaly * (Math.PI / 180);

            if (e < 1)
            { //elliptical orbits
                double cosE = (e + Math.Cos(trueAnomaly)) / (1 + e * Math.Cos(trueAnomaly));
                double sinE = Math.Sqrt(1 - (cosE * cosE));
                if (trueAnomaly > Math.PI)
                    sinE *= -1;

                return JUtil.ClampRadiansTwoPi(Math.Atan2(sinE, cosE));
            }
            else
            {  //hyperbolic orbits
                double coshE = (e + Math.Cos(trueAnomaly)) / (1 + e * Math.Cos(trueAnomaly));
                if (coshE < 1)
                    throw new ArgumentException("OrbitExtensions.GetEccentricAnomalyAtTrueAnomaly: True anomaly of " + trueAnomaly + " radians is not attained by orbit with eccentricity " + o.eccentricity);

                double E = JUtil.Acosh(coshE);
                if (trueAnomaly > Math.PI)
                    E *= -1;

                return E;
            }
        }
        //Originally by Zool, revised by The_Duck
        //Converts an eccentric anomaly into a mean anomaly.
        //For an elliptical orbit, the returned value is between 0 and 2pi
        //For a hyperbolic orbit, the returned value is any number
        public static double GetMeanAnomalyAtEccentricAnomaly(this Orbit o, double E)
        {
            double e = o.eccentricity;
            if (e < 1)
            { //elliptical orbits
                return JUtil.ClampRadiansTwoPi(E - (e * Math.Sin(E)));
            } //hyperbolic orbits
            return (e * Math.Sinh(E)) - E;
        }
        //Converts a true anomaly into a mean anomaly (via the intermediate step of the eccentric anomaly)
        //For elliptical orbits, the output is between 0 and 2pi
        //For hyperbolic orbits, the output can be any number
        //NOTE: For a hyperbolic orbit, if a true anomaly is requested that does not exist (a true anomaly
        //past the true anomaly of the asymptote) then an ArgumentException is thrown
        public static double GetMeanAnomalyAtTrueAnomaly(this Orbit o, double trueAnomaly)
        {
            return o.GetMeanAnomalyAtEccentricAnomaly(o.GetEccentricAnomalyAtTrueAnomaly(trueAnomaly));
        }
        //NOTE: this function can throw an ArgumentException, if o is a hyperbolic orbit with an eccentricity
        //large enough that it never attains the given true anomaly
        public static double TimeOfTrueAnomaly(this Orbit o, double trueAnomaly, double UT)
        {
            return o.UTAtMeanAnomaly(o.GetMeanAnomalyAtEccentricAnomaly(o.GetEccentricAnomalyAtTrueAnomaly(trueAnomaly)), UT);
        }
        //Returns the next time at which a will cross its ascending node with b.
        //For elliptical orbits this is a time between UT and UT + a.period.
        //For hyperbolic orbits this can be any time, including a time in the past if
        //the ascending node is in the past.
        //NOTE: this function will throw an ArgumentException if a is a hyperbolic orbit and the "ascending node"
        //occurs at a true anomaly that a does not actually ever attain
        public static double TimeOfAscendingNode(this Orbit a, Orbit b, double UT)
        {
            if (a.eccentricity >= 1.0)
            {
                return UT;
            }
            else
            {
                return a.TimeOfTrueAnomaly(a.AscendingNodeTrueAnomaly(b), UT);
            }
        }
        //Returns the next time at which a will cross its descending node with b.
        //For elliptical orbits this is a time between UT and UT + a.period.
        //For hyperbolic orbits this can be any time, including a time in the past if
        //the descending node is in the past.
        //NOTE: this function will throw an ArgumentException if a is a hyperbolic orbit and the "descending node"
        //occurs at a true anomaly that a does not actually ever attain
        public static double TimeOfDescendingNode(this Orbit a, Orbit b, double UT)
        {
            if (a.eccentricity >= 1.0)
            {
                return UT;
            }
            else
            {
                return a.TimeOfTrueAnomaly(a.DescendingNodeTrueAnomaly(b), UT);
            }
        }
        //Returns the next time at which the orbiting object will cross the equator
        //moving northward, if o is east-moving, or southward, if o is west-moving.
        //For elliptical orbits this is a time between UT and UT + o.period.
        //For hyperbolic orbits this can by any time, including a time in the past if the
        //ascending node is in the past.
        //NOTE: this function will throw an ArgumentException if o is a hyperbolic orbit and the
        //"ascending node" occurs at a true anomaly that o does not actually ever attain.
        public static double TimeOfAscendingNodeEquatorial(this Orbit o, double UT)
        {
            if (o.eccentricity >= 1.0)
            {
                return UT;
            }
            else
            {
                return o.TimeOfTrueAnomaly(o.AscendingNodeEquatorialTrueAnomaly(), UT);
            }
        }
        //Returns the next time at which the orbiting object will cross the equator
        //moving southward, if o is east-moving, or northward, if o is west-moving.
        //For elliptical orbits this is a time between UT and UT + o.period.
        //For hyperbolic orbits this can by any time, including a time in the past if the
        //descending node is in the past.
        //NOTE: this function will throw an ArgumentException if o is a hyperbolic orbit and the
        //"descending node" occurs at a true anomaly that o does not actually ever attain.
        public static double TimeOfDescendingNodeEquatorial(this Orbit o, double UT)
        {
            return o.TimeOfTrueAnomaly(o.DescendingNodeEquatorialTrueAnomaly(), UT);
        }
        //Computes the period of the phase angle between orbiting objects a and b.
        //This only really makes sense for approximately circular orbits in similar planes.
        //For noncircular orbits the time variation of the phase angle is only "quasiperiodic"
        //and for high eccentricities and/or large relative inclinations, the relative motion is
        //not really periodic at all.
        public static double SynodicPeriod(this Orbit a, Orbit b)
        {
            int sign = (Vector3d.Dot(a.SwappedOrbitNormal(), b.SwappedOrbitNormal()) > 0 ? 1 : -1); //detect relative retrograde motion
            return Math.Abs(1.0 / (1.0 / a.period - sign * 1.0 / b.period)); //period after which the phase angle repeats
        }
        //Computes the phase angle between two orbiting objects.
        //This only makes sence if a.referenceBody == b.referenceBody.
        public static double PhaseAngle(this Orbit a, Orbit b, double UT)
        {
            Vector3d normalA = a.SwappedOrbitNormal();
            Vector3d posA = a.SwappedRelativePositionAtUT(UT);
            Vector3d projectedB = Vector3d.Exclude(normalA, b.SwappedRelativePositionAtUT(UT));
            double angle = Vector3d.Angle(posA, projectedB);
            if (Vector3d.Dot(Vector3d.Cross(normalA, posA), projectedB) < 0)
            {
                angle = 360 - angle;
            }
            return angle;
        }
        //Computes the angle between two orbital planes. This will be a number between 0 and 180
        //Note that in the convention used two objects orbiting in the same plane but in
        //opposite directions have a relative inclination of 180 degrees.
        public static double RelativeInclination(this Orbit a, Orbit b)
        {
            return Math.Abs(Vector3d.Angle(a.SwappedOrbitNormal(), b.SwappedOrbitNormal()));
        }
        //Finds the next time at which the orbiting object will achieve a given radius
        //from the center of the primary.
        //If the given radius is impossible for this orbit, an ArgumentException is thrown.
        //For elliptical orbits this will be a time between UT and UT + period
        //For hyperbolic orbits this can be any time. If the given radius will be achieved
        //in the future then the next time at which that radius will be achieved will be returned.
        //If the given radius was only achieved in the past, then there are no guarantees
        //about which of the two times in the past will be returned.
        public static double NextTimeOfRadius(this Orbit o, double UT, double radius)
        {
            if (radius < o.PeR || (o.eccentricity < 1 && radius > o.ApR))
                throw new ArgumentException("OrbitExtensions.NextTimeOfRadius: given radius of " + radius + " is never achieved: o.PeR = " + o.PeR + " and o.ApR = " + o.ApR);

            double trueAnomaly1 = 180 / Math.PI * o.TrueAnomalyAtRadius(radius);
            double trueAnomaly2 = 360 - trueAnomaly1;
            double time1 = o.TimeOfTrueAnomaly(trueAnomaly1, UT);
            double time2 = o.TimeOfTrueAnomaly(trueAnomaly2, UT);
            if (time2 < time1 && time2 > UT)
                return time2;
            return time1;
        }

        public static Vector3d DeltaVToManeuverNodeCoordinates(this Orbit o, double UT, Vector3d dV)
        {
            return new Vector3d(Vector3d.Dot(o.RadialPlus(UT), dV),
                Vector3d.Dot(-o.NormalPlus(UT), dV),
                Vector3d.Dot(o.Prograde(UT), dV));
        }
    }
}
