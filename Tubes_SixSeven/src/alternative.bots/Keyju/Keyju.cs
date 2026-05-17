using System;
using System.Collections.Generic;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

public class Keyju : Bot
{
    // ── Target registry 
    private class TargetInfo
    {
        public int    Id;
        public double X, Y, Speed, Direction, Energy;
        public double Distance;
        public int    LastSeen;  // turn number
    }

    private readonly Dictionary<int, TargetInfo> _targets = new();
    private TargetInfo? _primary = null;      // current engagement target
    private int _turnCount = 0;

    // ── Movement state 
    private double _orbitDirection = 1.0;     // +1 CW, -1 CCW
    private int    _orbitFlipCooldown = 0;    // prevent rapid flip
    private bool   _ramMode = false;

    // ── Constants 
    private const double WALL_MARGIN        = 80.0;   // px from wall to start evasion
    private const double LOCK_LOST_TURNS    = 8;      // turns before we drop a lock
    private const double PREFERRED_DISTANCE = 250.0;  // orbit radius sweet-spot
    private const double RAM_THRESHOLD      = 5.0;    // enemy energy for ram-finish
    private const double FIRE_ARC_THRESHOLD = 4.5;    // degrees: max gun error to fire

    public static void Main(string[] args) => new Keyju().Start();
    public Keyju() : base(BotInfo.FromFile("Keyju.json")) { }

    // Main loop

    public override void Run()
    {

        while (IsRunning)
        {
            _turnCount++;

            // Purge stale targets
            PurgeOldTargets();

            // Select primary target (lowest energy, closest as tiebreak)
            SelectPrimaryTarget();

            // === RADAR ===
            SetRadar();

            // === MOVEMENT ===
            if (_ramMode && _primary != null)
                DoRamMovement();
            else
                DoOrbitMovement();

            // === FIRING ===
            // (gun turn + fire happen in OnScannedBot so we have fresh data)

            Go();
        }
    }

    // Radar: melee lock with overscan guarantee

    private void SetRadar()
    {
        if (_primary == null)
        {
            // No target — spin full speed to find someone
            RadarTurnRate = MaxRadarTurnRate;
            return;
        }

        // Compute bearing from radar to primary target
        double radarBearing = NormalizeRelativeAngle(RadarBearingTo(_primary.X, _primary.Y));

        // Overscan: add a small extra rotation past target so scan arc
        // always crosses the target, guaranteeing an OnScannedBot event.
        double overscan = Math.Sign(radarBearing) * 6.0;
        RadarTurnRate = Clamp(radarBearing + overscan, -MaxRadarTurnRate, MaxRadarTurnRate);
    }

    // Orbit movement — stay perpendicular at PREFERRED_DISTANCE

    private void DoOrbitMovement()
    {
        if (_primary == null)
        {
            // Default wander
            TurnRate   = 5.0;
            TargetSpeed = MaxSpeed * 0.7;
            return;
        }

        double dist    = DistanceTo(_primary.X, _primary.Y);
        double bearing = BearingTo(_primary.X, _primary.Y);

        // Perpendicular orbit angle (+ or - 90° from bearing to enemy)
        double orbitAngle = bearing + _orbitDirection * 90.0;
        orbitAngle = NormalizeRelativeAngle(orbitAngle);

        // Speed: approach if too far, back off if too close
        double speedFactor = 1.0;
        if (dist < PREFERRED_DISTANCE * 0.6)       speedFactor = -0.5;   // reverse
        else if (dist < PREFERRED_DISTANCE * 0.85)  speedFactor = 0.5;
        else if (dist > PREFERRED_DISTANCE * 1.5)   speedFactor = 1.2;

        double desiredSpeed = MaxSpeed * Clamp(speedFactor, -1.0, 1.2);
        TargetSpeed = desiredSpeed;

        // Body turn towards orbit angle (clamped by physics)
        TurnRate = Clamp(orbitAngle, -MaxTurnRate, MaxTurnRate);

        // Wall avoidance — smooth steer away from walls
        ApplyWallAvoidance();

        // Randomly flip orbit direction occasionally to be less predictable
        if (_orbitFlipCooldown > 0) _orbitFlipCooldown--;
        if (_orbitFlipCooldown == 0 && new Random().NextDouble() < 0.008)
        {
            _orbitDirection *= -1;
            _orbitFlipCooldown = 40;
        }
    }


    // Ram movement — charge into weakened enemy for kill bonus (+30%)

    private void DoRamMovement()
    {
        if (_primary == null) return;

        double bearing = BearingTo(_primary.X, _primary.Y);
        TurnRate    = Clamp(bearing, -MaxTurnRate, MaxTurnRate);
        TargetSpeed = MaxSpeed;
    }


    // Wall avoidance via angular push

    private void ApplyWallAvoidance()
    {
        double x = X, y = Y;
        double w = ArenaWidth, h = ArenaHeight;

        double pushX = 0, pushY = 0;
        if (x < WALL_MARGIN)               pushX = +1.0;
        else if (x > w - WALL_MARGIN)      pushX = -1.0;
        if (y < WALL_MARGIN)               pushY = +1.0;
        else if (y > h - WALL_MARGIN)      pushY = -1.0;

        if (pushX == 0 && pushY == 0) return;  // not near wall

        // Bearing towards arena centre-ish
        double safeBearing = Math.Atan2(pushX, pushY) * (180.0 / Math.PI);
        double relBearing   = NormalizeRelativeAngle(safeBearing - Direction);

        // Blend: if the push is urgent, override TurnRate strongly
        double urgency = 1.0 - Math.Min(Math.Min(x, w - x), Math.Min(y, h - y)) / WALL_MARGIN;
        TurnRate += relBearing * urgency * 0.5;
        TurnRate  = Clamp(TurnRate, -MaxTurnRate, MaxTurnRate);
    }

    // Scanned Bot — update registry + fire
  
    public override void OnScannedBot(ScannedBotEvent e)
    {
        // Update or create target entry
        if (!_targets.TryGetValue(e.ScannedBotId, out TargetInfo? t))
        {
            t = new TargetInfo { Id = e.ScannedBotId };
            _targets[e.ScannedBotId] = t;
        }

        t.X         = e.X;
        t.Y         = e.Y;
        t.Speed     = e.Speed;
        t.Direction = e.Direction;
        t.Energy    = e.Energy;
        t.Distance  = DistanceTo(e.X, e.Y);
        t.LastSeen  = _turnCount;

        // If this is our primary, engage immediately
        if (_primary != null && e.ScannedBotId == _primary.Id)
        {
            _primary = t;
            EngagePrimary();
        }
    }


    // Engagement logic — prediction + gun turn + fire
  
    private void EngagePrimary()
    {
        if (_primary == null) return;

        // ── Ram mode check ──
        _ramMode = (_primary.Energy <= RAM_THRESHOLD && _primary.Distance < 200);
        if (_ramMode) return;  // just charge; no shooting needed

        // ── Power selection ──
        double firePower = SelectFirePower(_primary);
        double bulletSpeed = (20 - 3 * firePower);  // TankRoyale formula

        // ── Iterative bullet travel prediction ──
        double[] pred = PredictImpact(_primary, bulletSpeed);
        double predX = pred[0], predY = pred[1];

        // ── Gun alignment ──
        double gunBearing = NormalizeRelativeAngle(GunBearingTo(predX, predY));
        GunTurnRate = Clamp(gunBearing, -MaxGunTurnRate, MaxGunTurnRate);

        // ── Fire only when aligned and gun is cool ──
        if (Math.Abs(gunBearing) < FIRE_ARC_THRESHOLD && GunHeat == 0)
            SetFire(firePower);
    }

    // ── Dynamic fire power
    private double SelectFirePower(TargetInfo t)
    {
        // Never spend energy we don't have
        double maxAffordable = Math.Min(3.0, Energy / 4.0);

        double power;
        if (t.Distance < 100)       power = 3.0;       // close range: maximum
        else if (t.Distance < 200)  power = 2.0;
        else if (t.Distance < 400)  power = 1.5;
        else                         power = 1.0;

        // Finish low-energy targets quickly
        if (t.Energy < 15)  power = Math.Min(power + 0.5, 3.0);

        // Don't suicide-fire
        if (Energy < 20)    power = 1.0;

        return Clamp(power, 0.1, maxAffordable);
    }

    // ── Iterative linear prediction 
    private double[] PredictImpact(TargetInfo t, double bulletSpeed)
    {
        double tx = t.X, ty = t.Y;
        double dirRad = t.Direction * (Math.PI / 180.0);
        double dx = t.Speed * Math.Sin(dirRad);
        double dy = t.Speed * Math.Cos(dirRad);

        // Iterate up to 100 ticks; stop when bullet would reach the position
        for (int i = 0; i < 100; i++)
        {
            tx += dx;
            ty += dy;
            double travelTime = i + 1;
            double bulletDist = bulletSpeed * travelTime;
            if (bulletDist >= DistanceTo(tx, ty))
                break;

            // Basic arena boundary reflection for better accuracy
            if (tx < 18 || tx > ArenaWidth - 18)  dx = -dx;
            if (ty < 18 || ty > ArenaHeight - 18) dy = -dy;
        }

        return new double[] { tx, ty };
    }


    // Target selection — lowest energy first; distance as tiebreak
 
    private void SelectPrimaryTarget()
    {
        TargetInfo? best = null;

        foreach (var kv in _targets)
        {
            var t = kv.Value;
            if (best == null) { best = t; continue; }

            bool lowerEnergy = t.Energy < best.Energy - 5;
            bool sameEnergy  = Math.Abs(t.Energy - best.Energy) <= 5;
            bool closer      = t.Distance < best.Distance;

            if (lowerEnergy || (sameEnergy && closer))
                best = t;
        }

        _primary = best;
    }

    // Purge targets not seen for LOCK_LOST_TURNS

    private void PurgeOldTargets()
    {
        var stale = new List<int>();
        foreach (var kv in _targets)
            if (_turnCount - kv.Value.LastSeen > LOCK_LOST_TURNS)
                stale.Add(kv.Key);

        foreach (var id in stale)
            _targets.Remove(id);
    }


    // Events

    public override void OnHitByBullet(HitByBulletEvent e)
    {
        // Quick dodge: flip orbit direction immediately
        _orbitDirection *= -1;
        _orbitFlipCooldown = 20;
        TargetSpeed = MaxSpeed;
    }

    public override void OnHitWall(HitWallEvent e)
    {
        // Reverse and turn towards centre
        TargetSpeed = -TargetSpeed * 0.6;
        double cx = ArenaWidth / 2.0, cy = ArenaHeight / 2.0;
        double bearingToCenter = NormalizeRelativeAngle(BearingTo(cx, cy));
        TurnRate = Clamp(bearingToCenter, -MaxTurnRate, MaxTurnRate);
    }

    public override void OnHitBot(HitBotEvent e)
    {
        if (_primary != null && e.VictimId == _primary.Id && _ramMode)
        {
            // We intentionally rammed — keep going
            TargetSpeed = MaxSpeed;
        }
        else
        {
            // Unintentional collision — escape
            TargetSpeed = -MaxSpeed;
            _orbitDirection *= -1;
        }
    }

    public override void OnBotDeath(BotDeathEvent e)
    {
        // Remove dead bot from registry
        _targets.Remove(e.VictimId);
        if (_primary != null && _primary.Id == e.VictimId)
            _primary = null;
    }


    // Utility

    private static double Clamp(double v, double min, double max)
        => Math.Max(min, Math.Min(max, v));
}