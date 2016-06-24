﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using HexapiBackground.Gps;
using HexapiBackground.Helpers;
using HexapiBackground.IK;

namespace HexapiBackground.Navigation
{
    internal sealed class Navigator
    {
        private readonly IkController _ikController;
        private readonly Gps.Gps _gps;
        private bool _gpsNavigationEnabled;
        private List<LatLon> _waypoints;

        internal Navigator(IkController ikController, Gps.Gps gps)
        {
            _ikController = ikController;
            _gps = gps;
        }
            
        internal async Task EnableGpsNavigation()
        {
            if (_gpsNavigationEnabled)
                return;

            _waypoints = await GpsExtensions.LoadWaypoints();

            _gpsNavigationEnabled = true;

            await Display.Write($"{_waypoints.Count} waypoints");

            foreach (var wp in _waypoints)
            {
                if (wp.Lat == 0 || wp.Lon == 0)
                    continue;

                await NavigateToWaypoint(wp);

                if (!_gpsNavigationEnabled)
                    break;
            }
        }

        internal void DisableGpsNavigation()
        {
            _gpsNavigationEnabled = false;
        }
        
        internal async Task<bool> NavigateToWaypoint(LatLon currentWaypoint)
        {
            var distanceHeading = GpsExtensions.GetDistanceAndHeadingToDestination(_gps.CurrentLatLon.Lat, _gps.CurrentLatLon.Lon, currentWaypoint.Lat, currentWaypoint.Lon);
            var distanceToWaypoint = distanceHeading[0];
            var headingToWaypoint = distanceHeading[1];

            var _travelLengthX = 0D;
            var _travelLengthZ = 0D;
            var _travelRotationY = 0D;
            var _nomGaitSpeed = 50D;
            
            _travelLengthZ = -50;

            var turnDirection = "None";

            while (distanceToWaypoint > 10) //Inches
            {
                await Display.Write($"WP D/H {distanceToWaypoint}, {headingToWaypoint}", 1);
                await Display.Write($"{turnDirection} {_gps.CurrentLatLon.Heading}", 2);

                if (headingToWaypoint + 5 > 359 && Math.Abs(headingToWaypoint - _gps.CurrentLatLon.Heading) > 1)
                {
                    var tempHeading = (headingToWaypoint + 5) - 359;

                    if (_gps.CurrentLatLon.Heading > tempHeading)
                    {
                        turnDirection = "Right";
                        _travelRotationY = -1;
                    }
                    else
                    {
                        turnDirection = "Left";
                        _travelRotationY = 1;
                    }
                }
                else if (headingToWaypoint - 5 < 1 && Math.Abs(headingToWaypoint - _gps.CurrentLatLon.Heading) > 1)
                {
                    var tempHeading = (headingToWaypoint + 359) - 5;

                    

                    if (_gps.CurrentLatLon.Heading < tempHeading)
                    {
                        turnDirection = "Right";
                        _travelRotationY = 1;
                    }
                    else
                    {
                        turnDirection = "Left";
                        _travelRotationY = -1;
                    }
                }
                else if (_gps.CurrentLatLon.Heading > headingToWaypoint - 5 && _gps.CurrentLatLon.Heading < headingToWaypoint + 5)
                {
                    _travelRotationY = 0;
                    turnDirection = "None";
                }
                else if (headingToWaypoint > _gps.CurrentLatLon.Heading + 20)
                {
                    if (_gps.CurrentLatLon.Heading - headingToWaypoint > 180)
                    {
                        turnDirection = "Left+";
                        _travelRotationY = -2;
                    }
                    else
                    {
                        turnDirection = "Right+";
                        _travelRotationY = 2;
                    }
                }
                else if (headingToWaypoint > _gps.CurrentLatLon.Heading)
                {
                    if (_gps.CurrentLatLon.Heading - headingToWaypoint > 180)
                    {
                        turnDirection = "Left";
                        _travelRotationY = -1;
                    }
                    else
                    {
                        turnDirection = "Right";
                        _travelRotationY = 1;
                    }
                }
                else if (headingToWaypoint < _gps.CurrentLatLon.Heading - 20) //If it has a long ways to turn, go fast!
                {
                    if (_gps.CurrentLatLon.Heading - headingToWaypoint < 180)
                    {
                        turnDirection = "Left+";
                        _travelRotationY = -2;
                    }
                    else
                    {
                        turnDirection = "Right+";
                        _travelRotationY = 2; //Turn towards its right
                    }
                }
                else if (headingToWaypoint < _gps.CurrentLatLon.Heading)
                {
                    if (_gps.CurrentLatLon.Heading - headingToWaypoint < 180)
                    {
                        turnDirection = "Left";
                        _travelRotationY = -1;
                    }
                    else
                    {
                        turnDirection = "Right";
                        _travelRotationY = 1;
                    }
                }

                _ikController.RequestMovement(_nomGaitSpeed, _travelLengthX, _travelLengthZ, _travelRotationY);

                await Task.Delay(50);

                distanceHeading = GpsExtensions.GetDistanceAndHeadingToDestination(_gps.CurrentLatLon.Lat, _gps.CurrentLatLon.Lon, currentWaypoint.Lat, currentWaypoint.Lon);
                distanceToWaypoint = distanceHeading[0];
                headingToWaypoint = distanceHeading[1];

                if (!_gpsNavigationEnabled)
                    return false;
            }

            await Display.Write($"WP D/H {distanceToWaypoint}, {headingToWaypoint}", 1);
            await Display.Write($"Heading {_gps.CurrentLatLon.Heading}", 2);

            return true;
        }
    }
}