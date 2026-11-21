#!/usr/bin/env python3
"""
Generates dispatch.db - a realistic, internally consistent railway infrastructure maintenance dataset
(Atlantic Corridor Rail, a fictional operator).

Usage:
    python3 generate_db.py                 # anchor "today" = current date
    python3 generate_db.py --anchor 2026-11-11 --out dispatch.db --seed 42

The simulation:
  * 120 days of history, today, and 14 days of forward bookings.
  * Each technician works a timeline across their shift; jobs are placed by region,
    required skill/proficiency, daily capacity and travel time.
  * "Today" is frozen at a simulated clock (default 11:30). Jobs before that time are
    completed/in-progress, later ones are assigned/dispatched, and some are unassigned,
    overrunning or already breaching SLA - so the SLA-risk screen has a real story.

The API rebases every date on startup so the anchor day always becomes the real today.
Only the Python standard library is used.
"""
import argparse
import math
import os
import random
import re
import sqlite3
from datetime import date, datetime, time, timedelta

GENERATOR_VERSION = "1.0.0"
HISTORY_DAYS = 60
FUTURE_DAYS = 14
SIM_NOW_TIME = time(11, 30)

FIRST_NAMES = """James Mary Robert Patricia John Jennifer Michael Linda David Elizabeth William Barbara Richard Susan Joseph Jessica
Thomas Sarah Christopher Karen Charles Lisa Daniel Nancy Matthew Betty Anthony Sandra Mark Margaret Donald Ashley Steven Kimberly
Andrew Emily Paul Donna Joshua Michelle Kenneth Carol Kevin Amanda Brian Melissa George Deborah Timothy Stephanie Ronald Rebecca
Jason Sharon Edward Laura Jeffrey Cynthia Ryan Dorothy Jacob Amy Gary Kathleen Nicholas Angela Eric Shirley Jonathan Brenda Stephen
Emma Larry Pamela Justin Nicole Scott Anna Brandon Samantha Benjamin Katherine Samuel Christine Gregory Debra Alexander Rachel Patrick
Carolyn Frank Janet Raymond Maria Jack Olivia Dennis Heather Jerry Helen Tyler Catherine Aaron Diane Jose Julie Adam Victoria Nathan
Joyce Henry Lauren Zachary Kelly Douglas Christina Peter Ruth Kyle Joan Noah Virginia Ethan Judith Jeremy Evelyn Walter Hannah
Christian Andrea Keith Megan Roger Cheryl Terry Jacqueline Austin Madison Sean Teresa Gerald Abigail Carl Sophia Harold Martha Dylan
Sara Arthur Gloria Lawrence Janice Jordan Kathryn Jesse Ann Bryan Isabella Billy Judy Bruce Charlotte Gabriel Julia Joe Grace Logan
Amber Alan Alice Juan Jean Albert Denise Willie Frances Elijah Danielle Wayne Marilyn Randy Natalie Vincent Diana Mason Brittany
Roy Beverly Ralph Doris Bobby Kayla Russell Alexis Bradley Lori Philip Tiffany Eugene Marie Luis Priya Carlos Mei Ahmed Fatima
Diego Sofia Hiroshi Yuki Omar Aisha Mateo Valentina Wei Ling Rahul Ananya Kwame Amara Ivan Elena Mohammed Leila""".split()

LAST_NAMES = """Smith Johnson Williams Brown Jones Garcia Miller Davis Rodriguez Martinez Hernandez Lopez Gonzalez Wilson Anderson
Thomas Taylor Moore Jackson Martin Lee Perez Thompson White Harris Sanchez Clark Ramirez Lewis Robinson Walker Young Allen King
Wright Scott Torres Nguyen Hill Flores Green Adams Nelson Baker Hall Rivera Campbell Mitchell Carter Roberts Gomez Phillips Evans
Turner Diaz Parker Cruz Edwards Collins Reyes Stewart Morris Morales Murphy Cook Rogers Gutierrez Ortiz Morgan Cooper Peterson
Bailey Reed Kelly Howard Ramos Kim Cox Ward Richardson Watson Brooks Chavez Wood James Bennett Gray Mendoza Ruiz Hughes Price
Alvarez Castillo Sanders Patel Myers Long Ross Foster Jimenez Powell Jenkins Perry Russell Sullivan Bell Coleman Butler Henderson
Barnes Gonzales Fisher Vasquez Simmons Romero Jordan Patterson Alexander Hamilton Graham Reynolds Griffin Wallace Moreno West Cole
Hayes Bryant Herrera Gibson Ellis Tran Medina Aguilar Stevens Murray Ford Castro Marshall Owens Harrison Fernandez McDonald Woods
Washington Kennedy Wells Vargas Henry Chen Freeman Webb Tucker Guzman Burns Crawford Olson Simpson Porter Hunter Gordon Mendez
Silva Shaw Snyder Mason Dixon Munoz Hunt Hicks Holmes Palmer Wagner Black Robertson Boyd Rose Stone Salazar Fox Warren Mills
Meyer Rice Schmidt Garza Daniels Ferguson Nichols Stephens Soto Weaver Ryan Gardner Payne Grant Dunn Kelley Spencer Hawkins Arnold
Pierce Vazquez Hansen Peters Santos Hart Bradley Knight Elliott Cunningham Duncan Armstrong Hudson Carroll Lane Riley Andrews
Alvarado Ray Delgado Berry Perkins Hoffman Johnston Matthews Pena Richards Contreras Willis Carpenter Lawrence Sandoval Okafor
Nakamura Singh Kowalski Novak Haddad Abara Lindqvist OBrien""".split()

STREET_NAMES = """Main Oak Pine Maple Cedar Elm Washington Lake Hill Park Walnut Spring North Ridge Church Willow Mill
Sunset Jackson Cherry Highland Franklin Lincoln River Madison Center Chestnut Meadow Forest Valley Union Liberty Adams Jefferson
Prospect Grove Broad Market Water High Bridge Commerce Industrial Harbor Airport Canal Station Summit Orchard Railroad""".split()
STREET_SUFFIX = ["St", "Ave", "Blvd", "Rd", "Dr", "Ln", "Way", "Ct", "Pkwy", "Pl"]

COMPANY_A = """Apex Summit Pinnacle Harbor Keystone Liberty Evergreen Riverside Granite Northstar Bluewater Ironwood Silverline
Crestview Lakeshore Meridian Cornerstone Heritage Frontier Pioneer Redwood Sterling Beacon Horizon Oakridge Cascade Atlas Vertex
Prairie Coastal Metro Union Capital Gateway Valley Highland Brightway Clearwater Golden Patriot Eagle Falcon Titan Unity Vista""".split()
COMPANY_B = """Medical Center|Logistics|Foods|Properties|Manufacturing|Hospitality Group|Retail|Data Center|Labs|School District|
Senior Living|Distribution|Hotels|Fitness|Dental Group|Credit Union|Bank|Pharmacy|Auto Group|Cold Storage|Brewing Co|
Apartments|Plaza|Office Park|Grocers|Clinic|University|Church|Restaurants|Storage|Cinemas|Bakery|Supply Co|Industries""".replace("\n", "").split("|")

REGIONS = [
    # code, name, tz, lat, lng, depots [(city, state, zip, lat, lng)]  - Atlantic Corridor Rail (fictional operator) territories
    ("NEN", "NEC North (Boston-New Haven)", "America/New_York", 41.82, -71.41, [("Boston", "MA", "02118", 42.342, -71.068), ("Providence", "RI", "02903", 41.829, -71.413), ("New Haven", "CT", "06519", 41.297, -72.927)]),
    ("NYD", "NEC New York Division", "America/New_York", 40.73, -74.17, [("Queens", "NY", "11101", 40.749, -73.926), ("Newark", "NJ", "07105", 40.735, -74.164), ("Trenton", "NJ", "08609", 40.218, -74.754)]),
    ("NES", "NEC South (Philadelphia-Washington)", "America/New_York", 39.74, -75.55, [("Philadelphia", "PA", "19104", 39.956, -75.182), ("Wilmington", "DE", "19801", 39.737, -75.551), ("Baltimore", "MD", "21201", 39.307, -76.616)]),
    ("KEY", "Keystone Line", "America/New_York", 40.05, -76.31, [("Harrisburg", "PA", "17101", 40.262, -76.878), ("Lancaster", "PA", "17602", 40.054, -76.308), ("Paoli", "PA", "19301", 40.043, -75.483)]),
    ("EMP", "Empire Corridor", "America/New_York", 41.71, -73.94, [("Rensselaer", "NY", "12144", 42.641, -73.741), ("Poughkeepsie", "NY", "12601", 41.707, -73.937), ("Croton-on-Hudson", "NY", "10520", 41.190, -73.882)]),
    ("SPG", "Springfield Line", "America/New_York", 41.77, -72.68, [("Springfield", "MA", "01103", 42.106, -72.593), ("Hartford", "CT", "06103", 41.768, -72.682), ("Meriden", "CT", "06450", 41.538, -72.795)]),
]

# Lines (stored as Customers): name, CustomerType (Enterprise=mainline, Commercial=branch), Tier (Platinum=Tier 1 ... Standard=Tier 3), stations
LINES = {
    "NEN": [("Boston Subdivision", "Enterprise", "Platinum", ["Boston South Station", "Back Bay", "Route 128", "Mansfield", "Attleboro", "Providence"]),
            ("Shore Line Subdivision", "Enterprise", "Platinum", ["Kingston", "Westerly", "Mystic", "New London", "Old Saybrook", "New Haven Union Station"]),
            ("Franklin Branch", "Commercial", "Gold", ["Readville", "Norwood Central", "Walpole", "Franklin"])],
    "NYD": [("New York Division Mainline", "Enterprise", "Platinum", ["New York Penn Station", "Secaucus Junction", "Newark Penn Station", "Newark Airport", "Elizabeth", "Rahway", "Metropark", "New Brunswick", "Princeton Junction", "Trenton"]),
            ("Hell Gate Line", "Enterprise", "Gold", ["Sunnyside", "New Rochelle", "Stamford", "Bridgeport"]),
            ("Morristown Branch", "Commercial", "Standard", ["Summit", "Madison", "Morristown", "Dover"])],
    "NES": [("Philadelphia Subdivision", "Enterprise", "Platinum", ["Philadelphia 30th Street", "North Philadelphia", "Cornwells Heights", "Bristol"]),
            ("Delmarva Mainline", "Enterprise", "Gold", ["Chester", "Wilmington", "Newark DE", "Aberdeen"]),
            ("Washington Terminal District", "Enterprise", "Platinum", ["Baltimore Penn Station", "BWI Airport", "New Carrollton", "Washington Union Station"])],
    "KEY": [("Harrisburg Line East", "Enterprise", "Gold", ["Paoli", "Exton", "Downingtown", "Coatesville", "Parkesburg"]),
            ("Keystone West", "Commercial", "Gold", ["Lancaster", "Mount Joy", "Elizabethtown", "Middletown", "Harrisburg"])],
    "EMP": [("Hudson Subdivision", "Enterprise", "Gold", ["Yonkers", "Croton-Harmon", "Poughkeepsie", "Rhinecliff", "Hudson"]),
            ("Albany Terminal", "Commercial", "Standard", ["Albany-Rensselaer", "Schenectady", "Amsterdam", "Saratoga Springs"])],
    "SPG": [("Springfield Subdivision", "Enterprise", "Gold", ["Wallingford", "Meriden", "Berlin", "Hartford", "Windsor", "Windsor Locks", "Springfield"]),
            ("Valley Branch", "Commercial", "Standard", ["Holyoke", "Northampton", "Greenfield"])],
}
ELECTRIFIED = {"NEN", "NYD", "NES", "KEY"}

SKILLS = [
    # code, name, category, requiresCert
    ("TRK", "Track Maintenance", "Track", 0),
    ("WELD", "Rail Welding (Thermite)", "Track", 1),
    ("SWT", "Switches & Turnouts", "Track", 1),
    ("GEOM", "Track Inspection & Geometry", "Track", 1),
    ("SIG", "Signal Maintenance", "Signals", 1),
    ("PTC", "Train Control / PTC", "Signals", 1),
    ("XING", "Grade Crossing Systems", "Signals", 1),
    ("CAT", "Overhead Catenary", "Power", 1),
    ("TPS", "Traction Power & Substations", "Power", 1),
    ("COMM", "Communications & Radio", "Communications", 0),
    ("SELEC", "Station Electrical", "Stations", 1),
    ("VT", "Elevators & Escalators", "Stations", 1),
    ("PIS", "Passenger Information Systems", "Stations", 0),
    ("FAC", "Station Facilities & HVAC", "Facilities", 0),
    ("STR", "Bridges & Structures", "Structures", 1),
    ("ROW", "Right-of-Way & Drainage", "Structures", 0),
]

JOB_TYPES = [
    # code, name, category, skill code, min proficiency, duration, cost estimate
    ("TRK-BRK", "Broken Rail", "Emergency", "TRK", 3, 180, 4200.0),
    ("TRK-DEF", "Rail Defect Remediation", "Repair", "TRK", 2, 150, 2600.0),
    ("TRK-TIE", "Tie & Fastener Renewal", "Maintenance", "TRK", 1, 240, 3100.0),
    ("WLD-REP", "Field Weld Repair", "Repair", "WELD", 3, 120, 1800.0),
    ("WLD-PRG", "Planned Thermite Welding", "Maintenance", "WELD", 2, 180, 2200.0),
    ("SWT-FAIL", "Switch Failure - Points Not Detecting", "Emergency", "SWT", 3, 120, 3200.0),
    ("SWT-PM", "Turnout Inspection & Lubrication", "Inspection", "SWT", 1, 90, 900.0),
    ("SWT-REP", "Switch Point / Frog Repair", "Repair", "SWT", 3, 180, 3800.0),
    ("GEO-INS", "Track Geometry Walk Inspection", "Inspection", "GEOM", 2, 120, 700.0),
    ("GEO-DEF", "Geometry Defect Follow-up", "Repair", "GEOM", 2, 120, 1400.0),
    ("SIG-DARK", "Signal Dark / Restrictive Aspect", "Emergency", "SIG", 3, 120, 2400.0),
    ("SIG-TC", "Track Circuit Failure", "Repair", "SIG", 3, 150, 2100.0),
    ("SIG-PM", "Signal Periodic Test", "Inspection", "SIG", 2, 90, 650.0),
    ("PTC-WIU", "PTC Wayside Interface Fault", "Repair", "PTC", 3, 120, 2300.0),
    ("PTC-INS", "Transponder / WIU Install", "Install", "PTC", 3, 240, 5200.0),
    ("XNG-MAL", "Crossing Gate Malfunction", "Emergency", "XING", 3, 120, 2600.0),
    ("XNG-TST", "Crossing Warning System Test", "Inspection", "XING", 2, 90, 600.0),
    ("CAT-DWN", "Catenary Wire Down", "Emergency", "CAT", 3, 240, 7800.0),
    ("CAT-REP", "Catenary Hardware Repair", "Repair", "CAT", 2, 180, 3400.0),
    ("CAT-INS", "Catenary Patrol Inspection", "Inspection", "CAT", 2, 120, 800.0),
    ("TPS-TRIP", "Substation Breaker Trip", "Emergency", "TPS", 3, 150, 4600.0),
    ("TPS-PM", "Substation Preventive Maintenance", "Maintenance", "TPS", 2, 180, 2100.0),
    ("COM-RAD", "Radio Coverage Fault", "Repair", "COMM", 2, 90, 1100.0),
    ("COM-INS", "Fiber / Comms Cabinet Install", "Install", "COMM", 2, 180, 2800.0),
    ("STE-PWR", "Station Power / Lighting Failure", "Repair", "SELEC", 2, 120, 1300.0),
    ("VT-OOS", "Elevator Out of Service", "Repair", "VT", 2, 120, 1600.0),
    ("VT-ESC", "Escalator Entrapment / Stop", "Emergency", "VT", 3, 90, 2000.0),
    ("VT-PM", "Elevator & Escalator PM", "Maintenance", "VT", 1, 90, 750.0),
    ("PIS-DSP", "PA / Display Failure", "Repair", "PIS", 1, 60, 600.0),
    ("FAC-HVAC", "Waiting Room HVAC Fault", "Repair", "FAC", 1, 90, 700.0),
    ("STR-INS", "Bridge / Structure Inspection", "Inspection", "STR", 2, 180, 1500.0),
    ("ROW-DRN", "Drainage / Washout Remediation", "Repair", "ROW", 1, 180, 1900.0),
]

# Tier -> priority -> (response, resolution, at-risk threshold, penalty)
SLA = {
    "Platinum": {"Critical": (120, 360, 45, 2500.0), "High": (240, 720, 60, 1200.0), "Medium": (480, 1440, 90, 500.0), "Low": (1440, 4320, 120, 150.0)},
    "Gold":     {"Critical": (180, 480, 45, 1500.0), "High": (360, 1080, 60, 800.0), "Medium": (720, 2160, 90, 300.0), "Low": (2880, 7200, 120, 100.0)},
    "Standard": {"Critical": (240, 720, 60, 750.0), "High": (480, 1440, 60, 400.0), "Medium": (1440, 2880, 120, 150.0), "Low": (4320, 10080, 180, 50.0)},
}

ASSET_TYPES = {
    "TRK": [("Track Section", ["Nucor", "Steel Dynamics", "voestalpine"])],
    "WELD": [("Rail Joint / Weld", ["Pandrol", "Holland", "Orgo-Thermit"])],
    "SWT": [("Turnout", ["voestalpine Nortrak", "Progress Rail"]), ("Switch Machine", ["Alstom", "Siemens Mobility", "Hitachi Rail"])],
    "GEOM": [("Curve / Superelevation Zone", ["ENSCO", "Plasser American"])],
    "SIG": [("Signal Head", ["Siemens Mobility", "Alstom", "Hitachi Rail"]), ("Track Circuit", ["Alstom", "Siemens Mobility"])],
    "PTC": [("Wayside Interface Unit", ["Wabtec", "Siemens Mobility"]), ("Transponder Set", ["Siemens Mobility", "Alstom"])],
    "XING": [("Crossing Gate Mechanism", ["Siemens Mobility", "Western-Cullen-Hayes"]), ("Crossing Predictor", ["Siemens Mobility", "Wabtec"])],
    "CAT": [("Catenary Span", ["Siemens Mobility", "Balfour Beatty", "Mosdorfer"]), ("Section Insulator", ["Arthur Flury", "Siemens Mobility"])],
    "TPS": [("Traction Substation", ["ABB", "Siemens Energy", "Hitachi Energy"]), ("DC Circuit Breaker", ["Secheron", "ABB"])],
    "COMM": [("Radio Base Station", ["Motorola Solutions", "L3Harris"]), ("Fiber Node", ["Cisco", "Nokia"])],
    "SELEC": [("Station Switchgear", ["Eaton", "Square D", "Siemens"])],
    "VT": [("Elevator", ["Otis", "Schindler", "KONE"]), ("Escalator", ["Otis", "Schindler", "KONE"])],
    "PIS": [("Passenger Info Display", ["Daktronics", "Trapeze", "Infodev"]), ("PA System", ["Bosch", "Harris"])],
    "FAC": [("Waiting Room HVAC", ["Trane", "Carrier", "Daikin"])],
    "STR": [("Bridge Span", ["American Bridge", "Phoenix Bridge"]), ("Retaining Wall", ["Reinforced Earth", "Hanson"])],
    "ROW": [("Culvert", ["Contech", "ADS"]), ("Drainage Ditch", ["ACR Engineering"])],
}

# site kind -> skills of assets found there
SITE_ASSET_SKILLS = {
    "Station": ["SELEC", "VT", "VT", "PIS", "FAC", "COMM"],
    "Interlocking": ["SWT", "SWT", "SIG", "SIG", "PTC"],
    "Track": ["TRK", "TRK", "WELD", "GEOM", "ROW", "CAT"],
    "Substation": ["TPS", "TPS", "CAT", "COMM"],
    "Crossing": ["XING", "XING", "SIG"],
    "Bridge": ["STR", "STR", "TRK"],
}
SITE_WEIGHTS = {"Station": 2, "Interlocking": 4, "Track": 3, "Substation": 2, "Crossing": 2, "Bridge": 1}

PART_CATALOG = {
    "Track": ["Rail 136RE (39 ft)", "Joint Bar Set 136RE", "Thermite Weld Kit", "Concrete Tie", "Timber Tie 7x9", "Pandrol e-Clip (box)",
              "Tie Plate", "Ballast (ton)", "Switch Point 136RE", "Frog No. 15 RBM", "Switch Heater Element", "Rail Anchor (box)"],
    "Signals": ["LED Signal Module Red", "LED Signal Module Green", "Vital Relay", "Insulated Joint Kit", "Bond Wire Kit", "Track Circuit Receiver Card",
                "Switch Machine Motor", "Point Detector Rod", "Signal Battery Bank", "Crossing Gate Arm 32 ft", "Crossing Flasher LED"],
    "Power": ["Contact Wire 107mm2 (ft)", "Messenger Wire (ft)", "Dropper Assembly", "Insulator Polymer 25kV", "Section Insulator Skid",
              "Vacuum Breaker Bottle", "Substation Protective Relay", "Surge Arrester 25kV"],
    "Communications": ["Radio Antenna UHF", "Fiber Patch Cord", "Fiber Splice Kit", "Radio Transceiver", "Cabinet UPS Battery"],
    "Stations": ["Elevator Door Operator", "Escalator Step", "Escalator Handrail", "LED Display Module", "PA Amplifier", "Station LED Fixture",
                 "Breaker 20A", "HVAC Contactor", "Air Filter 20x25"],
    "Structures": ["Bridge Bearing Pad", "Culvert Pipe 36in (ft)", "Riprap (ton)", "Geotextile (sq yd)", "Anchor Bolt Set"],
}

PRIORITY_WEIGHTS = {"Critical": 6, "High": 22, "Medium": 45, "Low": 27}
NOTE_TEXTS_DISPATCH = ["Track & time granted by RTC; window confirmed.", "Form D issued for Track 2 out of service.",
                       "Foul time requested; awaiting train clearance.", "Flagman assigned for adjacent-track protection.",
                       "Power desk notified; catenary de-energization scheduled.", "Parts confirmed in depot stock.",
                       "Repeat failure - previous fix did not hold.", "Temporary speed restriction 30 mph in effect pending repair.",
                       "Hi-rail access via nearest set-off point.", "Station manager notified of passenger impact."]
NOTE_TEXTS_TECH = ["On site, job briefing complete, beginning diagnostics.", "Replaced failed component, tested and returned to service.",
                   "Waiting for train clearance before fouling track.", "Recommend renewal - component at end of life.",
                   "Found loose bond wire, re-terminated and tested.", "Cleared debris and lubricated switch points.",
                   "Need second crew with boom truck for next visit.", "Restriction lifted, RTC notified.", "Ordered part, ETA 2 business days.",
                   "Inspection complete, report uploaded."]
HOLD_REASONS = ["Awaiting track outage", "Awaiting parts from depot", "Foul time denied - train traffic", "Requires second crew", "Awaiting power de-energization"]
SUMMARY_PREFIX = {"Repair": ["Defect reported by train crew", "Intermittent failure", "Wayside alarm received", "Degraded operation", "Repeat failure"],
                  "Maintenance": ["Scheduled program work", "Cyclic maintenance", "Planned renewal"],
                  "Install": ["Capital install", "Replacement install", "Upgrade install"],
                  "Inspection": ["Periodic inspection due", "Regulatory inspection", "Post-incident inspection"],
                  "Emergency": ["EMERGENCY - service suspended", "EMERGENCY - safety restriction in place", "EMERGENCY - trains held"]}

def fmt(dt):
    return dt.strftime("%Y-%m-%d %H:%M:%S") if dt else None


def fmt_d(d):
    return d.strftime("%Y-%m-%d") if d else None


def jitter(lat, lng, km):
    dlat = (random.random() - 0.5) * 2 * km / 111.0
    dlng = (random.random() - 0.5) * 2 * km / (111.0 * math.cos(math.radians(lat)))
    return round(lat + dlat, 6), round(lng + dlng, 6)


def km_between(a_lat, a_lng, b_lat, b_lng):
    dx = (a_lng - b_lng) * 111.0 * math.cos(math.radians((a_lat + b_lat) / 2))
    dy = (a_lat - b_lat) * 111.0
    return math.hypot(dx, dy)


def phone():
    return f"({random.randint(201, 989)}) {random.randint(200, 999)}-{random.randint(1000, 9999)}"


def weighted(d):
    return random.choices(list(d.keys()), weights=list(d.values()))[0]


def round_to(dt, minutes):
    discard = timedelta(minutes=dt.minute % minutes, seconds=dt.second)
    return dt - discard


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--anchor", help="anchor date YYYY-MM-DD (default: today)")
    ap.add_argument("--out", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "dispatch.db"))
    ap.add_argument("--seed", type=int, default=42)
    args = ap.parse_args()

    random.seed(args.seed)
    anchor = date.fromisoformat(args.anchor) if args.anchor else date.today()
    sim_now = datetime.combine(anchor, SIM_NOW_TIME)
    start_day = anchor - timedelta(days=HISTORY_DAYS)
    end_day = anchor + timedelta(days=FUTURE_DAYS)

    if os.path.exists(args.out):
        os.remove(args.out)
    con = sqlite3.connect(args.out)
    schema_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "schema.sql")
    with open(schema_path) as f:
        con.executescript(f.read())
    cur = con.cursor()

    cur.execute("INSERT INTO SeedMeta VALUES (1, ?, ?, ?)", (fmt_d(anchor), fmt(datetime.now()), GENERATOR_VERSION))

    # ---------------- Regions & depots ----------------
    depots = []  # dicts
    for rid, (code, name, tz, lat, lng, dlist) in enumerate(REGIONS, start=1):
        cur.execute("INSERT INTO Regions VALUES (?,?,?,?,?,?)", (rid, code, name, tz, lat, lng))
        for (city, st, zipc, dlat, dlng) in dlist:
            did = len(depots) + 1
            depots.append({"id": did, "region": rid, "lat": dlat, "lng": dlng, "city": city, "state": st})
            cur.execute("INSERT INTO Depots VALUES (?,?,?,?,?,?,?,?,?,?,?)",
                        (did, rid, f"{code}-{city[:3].upper()}", f"{city} MOW Base",
                         f"{random.randint(100, 9999)} {random.choice(STREET_NAMES)} {random.choice(STREET_SUFFIX)}",
                         city, st, zipc, dlat, dlng, phone()))
    region_ids = list(range(1, len(REGIONS) + 1))
    depots_by_region = {r: [d for d in depots if d["region"] == r] for r in region_ids}

    # ---------------- Dispatchers (40) ----------------
    used_emails = set()

    def email_for(first, last, domain):
        base = f"{first}.{last}".lower().replace("'", "")
        e, n = f"{base}@{domain}", 1
        while e in used_emails:
            n += 1
            e = f"{base}{n}@{domain}"
        used_emails.add(e)
        return e

    dispatchers = []
    shift_names = ["Early", "Day", "Day", "Late", "Night"]
    for i in range(1, 41):
        rid = region_ids[(i - 1) % len(region_ids)]
        fn, ln = random.choice(FIRST_NAMES), random.choice(LAST_NAMES)
        role = "Supervisor" if i <= 6 else ("SeniorDispatcher" if i <= 16 else "Dispatcher")
        dispatchers.append({"id": i, "region": rid, "name": f"{fn} {ln}"})
        cur.execute("INSERT INTO Dispatchers VALUES (?,?,?,?,?,?,?,?,?,?)",
                    (i, f"D{1000 + i}", fn, ln, email_for(fn, ln, "dispatch.acrail.example"), phone(), rid,
                     "Day" if role == "Supervisor" else random.choice(shift_names), role, 1))
    dispatchers_by_region = {r: [d for d in dispatchers if d["region"] == r] for r in region_ids}

    # ---------------- Skills & job types & SLA ----------------
    skill_id = {}
    for sid, (code, name, cat, cert) in enumerate(SKILLS, start=1):
        skill_id[code] = sid
        cur.execute("INSERT INTO Skills VALUES (?,?,?,?,?)", (sid, code, name, cat, cert))

    job_types = []
    for jid, (code, name, cat, skill, minp, dur, price) in enumerate(JOB_TYPES, start=1):
        job_types.append({"id": jid, "code": code, "name": name, "cat": cat, "skill": skill_id[skill], "skill_code": skill, "minp": minp, "dur": dur, "price": price})
        cur.execute("INSERT INTO JobTypes VALUES (?,?,?,?,?,?,?,?)", (jid, code, name, cat, skill_id[skill], minp, dur, price))

    sla_id = {}
    sid = 0
    for tier, pr in SLA.items():
        for prio, (resp, reso, risk, pen) in pr.items():
            sid += 1
            sla_id[(tier, prio)] = sid
            cur.execute("INSERT INTO SlaPolicies VALUES (?,?,?,?,?,?,?,?)", (sid, f"{tier} {prio}", tier, prio, resp, reso, risk, pen))

    # ---------------- Parts & inventory ----------------
    parts = []
    pid = 0
    for cat, names in PART_CATALOG.items():
        for nm in names:
            for variant in ["", " (OEM)"] if random.random() < 0.45 else [""]:
                pid += 1
                cost = round(random.uniform(4, 380), 2)
                parts.append({"id": pid, "cat": cat, "price": round(cost * random.uniform(1.35, 1.9), 2)})
                cur.execute("INSERT INTO Parts VALUES (?,?,?,?,?,?)", (pid, f"{cat[:3].upper()}-{10000 + pid}", nm + variant, cat, cost, parts[-1]["price"]))
    inv_rows = []
    for d in depots:
        for p in parts:
            inv_rows.append((d["id"], p["id"], random.randint(0, 60), random.randint(3, 12)))
    cur.executemany("INSERT INTO DepotInventory VALUES (?,?,?,?)", inv_rows)
    skill_to_partcat = {"TRK": "Track", "WELD": "Track", "SWT": "Track", "GEOM": "Track", "SIG": "Signals", "PTC": "Signals", "XING": "Signals",
                        "CAT": "Power", "TPS": "Power", "COMM": "Communications", "SELEC": "Stations", "VT": "Stations", "PIS": "Stations",
                        "FAC": "Stations", "STR": "Structures", "ROW": "Structures"}
    parts_by_cat = {}
    for p in parts:
        parts_by_cat.setdefault(p["cat"], []).append(p)

    # ---------------- Vehicles & technicians ----------------
    makes = [("Ford", "F-550 Hi-Rail", "Pickup"), ("Ford", "F-350 Hi-Rail", "Pickup"), ("Freightliner", "M2 Hi-Rail Boom Truck", "BoxTruck"),
             ("International", "MV Hi-Rail Welding Truck", "BoxTruck"), ("Chevrolet", "Silverado 3500HD Hi-Rail", "Pickup"),
             ("Ford", "Transit 350 Signal Van", "Van"), ("Ram", "ProMaster Comms Van", "Van"), ("Ford", "Explorer Track Inspector", "Car")]
    vehicles = []
    vid = 0
    for d in depots:
        for _ in range(25):
            vid += 1
            mk, md, vt = random.choice(makes)
            status = random.choices(["InService", "Maintenance", "OutOfService"], [92, 6, 2])[0]
            vehicles.append({"id": vid, "depot": d["id"], "status": status})
            cur.execute("INSERT INTO Vehicles VALUES (?,?,?,?,?,?,?,?,?)",
                        (vid, d["id"], f"{d['state']}-{random.randint(100, 999)}{random.choice('ABCDEFGHJKLMNPRSTUVWXYZ')}{random.choice('ABCDEFGHJKLMNPRSTUVWXYZ')}{vid:03d}",
                         mk, md, random.randint(2017, 2026), vt, status, random.randint(4000, 160000)))
    vehicles_by_depot = {d["id"]: [v for v in vehicles if v["depot"] == d["id"] and v["status"] == "InService"] for d in depots}

    # skill bundles: technicians tend to cluster by trade
    trade_bundles = [
        ["TRK", "WELD", "GEOM"], ["TRK", "SWT"], ["SWT", "SIG"], ["SIG", "PTC", "XING"], ["SIG", "XING"], ["PTC", "COMM"],
        ["CAT", "TPS"], ["CAT", "TRK"], ["TPS", "SELEC"], ["SELEC", "VT", "FAC"], ["VT", "PIS", "FAC"], ["PIS", "COMM"],
        ["STR", "ROW", "TRK"], ["ROW", "TRK", "GEOM"],
    ]
    levels = {"Apprentice": (1, 2, 24, 30), "TechI": (2, 3, 30, 38), "TechII": (3, 4, 36, 45), "Senior": (4, 5, 44, 55), "Lead": (4, 5, 52, 62)}
    techs = []
    tech_skill_rows = []
    tid = 0
    for d in depots:
        n = random.randint(21, 26)
        avail_vehicles = list(vehicles_by_depot[d["id"]])
        random.shuffle(avail_vehicles)
        for _ in range(n):
            tid += 1
            fn, ln = random.choice(FIRST_NAMES), random.choice(LAST_NAMES)
            level = random.choices(list(levels.keys()), [12, 26, 30, 22, 10])[0]
            pmin, pmax, rmin, rmax = levels[level]
            emp = random.choices(["Active", "OnLeave", "Inactive"], [93, 4, 3])[0]
            hlat, hlng = jitter(d["lat"], d["lng"], 35)
            vehicle = avail_vehicles.pop() if avail_vehicles and emp == "Active" else None
            shift_start_hour = random.choices([6, 7, 8, 9], [10, 45, 35, 10])[0]
            t = {"id": tid, "depot": d["id"], "region": d["region"], "emp": emp, "level": level, "name": f"{fn} {ln}",
                 "home": (hlat, hlng), "max": random.randint(4, 7) if level != "Apprentice" else random.randint(3, 5),
                 "start_hour": shift_start_hour, "skills": {}, "vehicle": vehicle["id"] if vehicle else None,
                 "weekend": random.random() < 0.3}
            bundle = list(random.choice(trade_bundles))
            if random.random() < 0.35:
                bundle.append(random.choice([s[0] for s in SKILLS]))
            for sc in set(bundle):
                prof = random.randint(pmin, pmax)
                t["skills"][skill_id[sc]] = prof
                cert_until = None
                if [s for s in SKILLS if s[0] == sc][0][3]:
                    cert_until = fmt_d(anchor + timedelta(days=random.randint(-20, 900)))
                tech_skill_rows.append((tid, skill_id[sc], prof, cert_until))
            techs.append(t)
            cur.execute("INSERT INTO Technicians VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                        (tid, f"T{20000 + tid}", fn, ln, email_for(fn, ln, "crews.acrail.example"), phone(), d["id"], t["vehicle"],
                         level, emp, "Offline", fmt_d(anchor - timedelta(days=random.randint(60, 4500))),
                         round(random.uniform(rmin, rmax), 2), t["max"], round(random.uniform(3.6, 5.0), 1),
                         hlat, hlng, None, None, None))
    cur.executemany("INSERT INTO TechnicianSkills VALUES (?,?,?,?)", tech_skill_rows)
    tech_by_id = {t["id"]: t for t in techs}

    # ---------------- Time off ----------------
    timeoff_rows = []
    off_days = {}  # (tech, date) -> True for approved
    toid = 0
    for t in techs:
        if t["emp"] == "Inactive":
            continue
        for _ in range(random.randint(0, 4)):
            toid += 1
            s = start_day + timedelta(days=random.randint(0, HISTORY_DAYS + FUTURE_DAYS + 30))
            length = random.choice([1, 1, 1, 2, 3, 5])
            e = s + timedelta(days=length - 1)
            typ = random.choices(["Vacation", "Sick", "Training", "Personal"], [45, 25, 15, 15])[0]
            if s > anchor:
                status = random.choices(["Approved", "Pending", "Denied"], [60, 32, 8])[0]
            else:
                status = random.choices(["Approved", "Denied"], [92, 8])[0]
            if typ == "Sick" and s > anchor:
                typ = "Personal"
            timeoff_rows.append((toid, t["id"], fmt_d(s), fmt_d(e), typ, status,
                                 {"Vacation": "Family trip", "Sick": "Illness", "Training": "Roadway Worker Protection recertification", "Personal": "Personal appointment"}[typ],
                                 fmt(datetime.combine(s, time(9)) - timedelta(days=random.randint(1, 40)))))
            if status == "Approved":
                dd = s
                while dd <= e:
                    off_days[(t["id"], dd)] = True
                    dd += timedelta(days=1)
        if t["emp"] == "OnLeave":
            toid += 1
            s = anchor - timedelta(days=random.randint(3, 20))
            e = anchor + timedelta(days=random.randint(10, 40))
            timeoff_rows.append((toid, t["id"], fmt_d(s), fmt_d(e), "Personal", "Approved", "Extended leave", fmt(datetime.combine(s, time(9)) - timedelta(days=10))))
            dd = s
            while dd <= e:
                off_days[(t["id"], dd)] = True
                dd += timedelta(days=1)
    cur.executemany("INSERT INTO TimeOffRequests VALUES (?,?,?,?,?,?,?,?)", timeoff_rows)

    # ---------------- Lines (Customers), locations (Sites), assets ----------------
    customers, sites, assets = [], [], []
    cust_rows, site_rows, asset_rows = [], [], []
    site_by_region = {r: [] for r in region_ids}
    assets_by_site = {}
    access_reqs = ["Track & time required", "Foul time only - adjacent track live", "Form D track outage required",
                   "Catenary de-energize & ground required", "Flagman required", "Station access via service corridor",
                   "Hi-rail set-off at nearest crossing", "Night window only (01:00-05:00)", "Confined space permit required", None]
    region_code = {rid: REGIONS[rid - 1][0] for rid in region_ids}
    cid = 0
    for rid in region_ids:
        rcode = region_code[rid]
        electrified = rcode in ELECTRIFIED
        for (lname, ctype, tier, stations) in LINES[rcode]:
            cid += 1
            cfn, cln = random.choice(FIRST_NAMES), random.choice(LAST_NAMES)
            customers.append({"id": cid, "tier": tier, "type": ctype})
            cust_rows.append((cid, f"LN-{100 + cid}", lname, ctype, tier, f"{cfn} {cln}", phone(),
                              f"{cfn}.{cln}@engineering.acrail.example".lower(), fmt(datetime.combine(anchor, time(10)) - timedelta(days=random.randint(900, 9000))), 1))
            abbr = "".join(w[0] for w in lname.split() if w[0].isalpha()).upper()
            ds = list(depots_by_region[rid])
            random.shuffle(ds)
            a, b = ds[0], ds[1]
            n = len(stations)
            mp = round(random.uniform(0, 30), 1)
            pts = []
            for i in range(n):
                f = i / max(1, n - 1)
                lat, lng = jitter(a["lat"] + (b["lat"] - a["lat"]) * f, a["lng"] + (b["lng"] - a["lng"]) * f, 3)
                pts.append((lat, lng, round(mp, 1)))
                mp += random.uniform(3, 9)

            def nearest_depot(lat, lng):
                return min(ds, key=lambda d: km_between(lat, lng, d["lat"], d["lng"]))

            def add_site(kind, name, lat, lng, milepost, near):
                sid = len(sites) + 1
                site = {"id": sid, "customer": cid, "region": rid, "lat": lat, "lng": lng, "tier": tier, "kind": kind, "name": name}
                sites.append(site)
                site_by_region[rid].append(site)
                dep = nearest_depot(lat, lng)
                if kind == "Station":
                    access = random.choice(["Station access via service corridor", "Platform edge - lookout required", None, None])
                elif kind in ("Track", "Interlocking", "Bridge"):
                    access = random.choice(access_reqs[:5] + access_reqs[6:8] + ([] if electrified else []))
                    if not electrified and access == "Catenary de-energize & ground required":
                        access = "Track & time required"
                elif kind == "Substation":
                    access = random.choice(["Switching order required", "Confined space permit required", "Key from power desk"])
                else:
                    access = random.choice(["Flagman required", "Road traffic control required", None])
                site_rows.append((sid, cid, rid, name, f"MP {milepost:.1f}", near, dep["state"], f"{random.randint(10000, 99999)}",
                                  lat, lng, access, phone()))
                skills_here = [s for s in SITE_ASSET_SKILLS[kind] if electrified or s not in ("CAT", "TPS")]
                assets_by_site[sid] = []
                for _ in range(random.randint(2, 4)):
                    sk = random.choice(skills_here)
                    atype, mfrs = random.choice(ASSET_TYPES[sk])
                    aid = len(assets) + 1
                    inst = anchor - timedelta(days=random.randint(200, 12000))
                    warranty = inst + timedelta(days=random.choice([730, 1825, 3650, 7300]))
                    status = random.choices(["Operational", "Degraded", "Down", "Retired"], [84, 10, 4, 2])[0]
                    assets.append({"id": aid, "site": sid, "skill": skill_id[sk]})
                    assets_by_site[sid].append(assets[-1])
                    mfr = random.choice(mfrs)
                    prefix = {"Station": "STN", "Interlocking": "IXL", "Track": "TRK", "Substation": "SUB", "Crossing": "XNG", "Bridge": "BRG"}[kind]
                    asset_rows.append((aid, sid, f"{rcode}-{prefix}-{10000 + aid}", atype, mfr,
                                       f"{mfr[:3].upper()}-{random.randint(100, 9999)}{random.choice('ABCDXZ')}",
                                       f"SN{random.randint(10000000, 99999999)}", fmt_d(inst), fmt_d(warranty), status))

            sub_n = 0
            for i, st_name in enumerate(stations):
                lat, lng, smp = pts[i]
                add_site("Station", f"{st_name} Station" if "Station" not in st_name else st_name, lat, lng, smp, st_name)
                if i == n - 1:
                    continue
                nlat, nlng, nmp = pts[i + 1]
                short = re.sub(r"[^A-Za-z]", "", st_name.split()[0]).upper()[:6]
                add_site("Interlocking", f"CP-{short} Interlocking", lat + (nlat - lat) * 0.1, lng + (nlng - lng) * 0.1, smp + (nmp - smp) * 0.1, st_name)
                for k in range(random.randint(3, 5)):
                    f0 = 0.15 + k * 0.17
                    m0 = smp + (nmp - smp) * f0
                    add_site("Track", f"{abbr} MP {m0:.1f}-{m0 + random.uniform(0.6, 1.8):.1f} Track {random.choice([1, 2, 2, 3])}",
                             lat + (nlat - lat) * f0, lng + (nlng - lng) * f0, m0, st_name)
                if random.random() < 0.45:
                    m0 = smp + (nmp - smp) * 0.55
                    add_site("Crossing", f"Grade Crossing {random.choice(STREET_NAMES)} {random.choice(['St', 'Rd', 'Ave'])} (MP {m0:.1f})",
                             lat + (nlat - lat) * 0.55, lng + (nlng - lng) * 0.55, m0, st_name)
                if random.random() < 0.35:
                    m0 = smp + (nmp - smp) * 0.7
                    add_site("Bridge", f"Undergrade Bridge MP {m0:.1f}", lat + (nlat - lat) * 0.7, lng + (nlng - lng) * 0.7, m0, st_name)
                if electrified and i % 2 == 0:
                    sub_n += 1
                    add_site("Substation", f"Substation {abbr}-{sub_n} ({st_name})", lat + (nlat - lat) * 0.3, lng + (nlng - lng) * 0.3,
                             smp + (nmp - smp) * 0.3, st_name)
    cur.executemany("INSERT INTO Customers VALUES (?,?,?,?,?,?,?,?,?,?)", cust_rows)
    cur.executemany("INSERT INTO Sites VALUES (?,?,?,?,?,?,?,?,?,?,?,?)", site_rows)
    cur.executemany("INSERT INTO Assets VALUES (?,?,?,?,?,?,?,?,?,?)", asset_rows)
    site_weight = {s["id"]: SITE_WEIGHTS[s["kind"]] for s in sites}
    job_types_by_skill = {}
    for jt in job_types:
        job_types_by_skill.setdefault(jt["skill"], []).append(jt)

    # ---------------- Shifts ----------------
    shift_rows = []
    on_shift = {}  # (date) -> list of (tech, start_dt, end_dt)
    shift_id = 0
    d = start_day
    while d <= end_day:
        weekday = d.weekday()
        on_shift[d] = []
        for t in techs:
            if t["emp"] == "Inactive":
                continue
            if weekday >= 5 and not (t["weekend"] and random.random() < 0.35):
                continue
            if weekday < 5 and random.random() < 0.06:  # rostered day off
                continue
            st = datetime.combine(d, time(t["start_hour"], 0))
            en = st + timedelta(hours=8, minutes=30)
            stype = "OnCall" if weekday >= 5 else ("Overtime" if random.random() < 0.04 else "Regular")
            if stype == "Overtime":
                en += timedelta(hours=2)
            shift_id += 1
            if (t["id"], d) in off_days:
                shift_rows.append((shift_id, t["id"], fmt_d(d), st.strftime("%H:%M:%S"), en.strftime("%H:%M:%S"), stype, "Cancelled", "Approved time off"))
                continue
            if d < anchor:
                status = "Absent" if random.random() < 0.015 else "Completed"
            elif d == anchor:
                status = "Absent" if random.random() < 0.02 else "InProgress"
            else:
                status = "Scheduled"
            shift_rows.append((shift_id, t["id"], fmt_d(d), st.strftime("%H:%M:%S"), en.strftime("%H:%M:%S"), stype, status,
                               "Called out" if status == "Absent" else None))
            if status != "Absent":
                on_shift[d].append((t, st, en))
        d += timedelta(days=1)
    cur.executemany("INSERT INTO Shifts VALUES (?,?,?,?,?,?,?,?)", shift_rows)

    # ---------------- Jobs ----------------
    job_rows, event_rows, note_rows, part_rows, resched_rows = [], [], [], [], []
    job_id = 0
    tech_today_state = {}  # tech id -> ('OnSite'|'EnRoute', site)

    def add_event(jid, etype, at, frm=None, to=None, tech=None, disp=None, details=None):
        event_rows.append((len(event_rows) + 1, jid, etype, frm, to, tech, disp, fmt(at), details))

    d = start_day
    while d <= end_day:
        is_weekend = d.weekday() >= 5
        days_ahead = (d - anchor).days
        for rid in region_ids:
            roster = [x for x in on_shift[d] if x[0]["region"] == rid]
            if not roster and not is_weekend:
                continue
            base = len(roster) * (random.uniform(1.6, 2.4) if is_weekend else random.uniform(2.6, 3.3))
            if days_ahead == 0:
                base *= 1.05
            elif days_ahead > 0:
                base *= max(0.18, 0.92 - days_ahead * 0.055)
            n_jobs = int(base)
            # timeline state per tech
            tl = {t["id"]: {"cursor": st, "end": en, "count": 0, "loc": (tech_by_id[t["id"]]["home"])} for (t, st, en) in roster}
            roster_by_skill = {}
            for (t, st, en) in roster:
                for sk, prof in t["skills"].items():
                    roster_by_skill.setdefault(sk, []).append((t, prof))

            region_sites = site_by_region[rid]
            weights = [site_weight[s["id"]] for s in region_sites]
            pending = []
            for _ in range(n_jobs):
                site = random.choices(region_sites, weights)[0]
                cust = customers[site["customer"] - 1]
                prio = weighted(PRIORITY_WEIGHTS) if not is_weekend else random.choices(["Critical", "High", "Medium"], [35, 50, 15])[0]
                site_assets = assets_by_site[site["id"]]
                asset = random.choice(site_assets) if site_assets and random.random() < 0.8 else None
                if asset and asset["skill"] in job_types_by_skill:
                    cands = job_types_by_skill[asset["skill"]]
                else:
                    cands = job_types
                if prio == "Critical":
                    em = [j for j in cands if j["cat"] == "Emergency"] or [j for j in cands if j["cat"] == "Repair"] or cands
                    jt = random.choice(em)
                elif prio in ("High",):
                    jt = random.choice([j for j in cands if j["cat"] in ("Repair", "Emergency")] or cands)
                else:
                    jt = random.choice([j for j in cands if j["cat"] not in ("Emergency",)] or cands)
                resp, reso, risk, pen = SLA[cust["tier"]][prio]
                if prio in ("Critical", "High"):
                    # reactive: created on the day (or previous evening), window begins shortly after
                    created = datetime.combine(d, time(6, 0)) + timedelta(minutes=random.randint(-300, 600))
                    if days_ahead == 0 and created > sim_now:
                        created = sim_now - timedelta(minutes=random.randint(5, 240))
                    if days_ahead > 0:
                        # future reactive jobs only exist as booked follow-ups
                        created = sim_now - timedelta(hours=random.randint(1, 30))
                        wstart = round_to(datetime.combine(d, time(random.randint(8, 14), 0)), 30)
                        wend = wstart + timedelta(hours=2)
                        response_due = wend
                    else:
                        wstart = round_to(created + timedelta(minutes=random.randint(20, 60)), 15)
                        wstart = max(wstart, datetime.combine(d, time(7, 0)))
                        response_due = created + timedelta(minutes=resp)
                        wend = max(wstart + timedelta(minutes=60), response_due)
                else:
                    wstart_hour = random.choice([8, 8, 9, 10, 11, 12, 13, 14, 15])
                    wstart = datetime.combine(d, time(wstart_hour, 0))
                    wend = wstart + timedelta(hours=random.choice([2, 2, 3, 4]))
                    created = datetime.combine(d, time(random.randint(8, 17), random.randint(0, 59))) - timedelta(days=random.randint(1, 12))
                    if created > sim_now:
                        created = sim_now - timedelta(hours=random.randint(1, 48))
                    response_due = wend
                resolution_due = response_due + timedelta(minutes=reso - resp)
                dur = int(jt["dur"] * random.uniform(0.8, 1.25) // 15 * 15) or 30
                pending.append({"site": site, "cust": cust, "prio": prio, "jt": jt, "asset": asset, "created": created,
                                "wstart": wstart, "wend": wend, "resp_due": response_due, "res_due": resolution_due, "dur": dur})
            pending.sort(key=lambda j: (j["wstart"], {"Critical": 0, "High": 1, "Medium": 2, "Low": 3}[j["prio"]]))

            # probability a job ends up assigned (future bookings are partially planned)
            if days_ahead < 0:
                assign_p = 0.995
            elif days_ahead == 0:
                assign_p = 0.9
            else:
                assign_p = max(0.35, 0.85 - (days_ahead - 1) * 0.04)

            for j in pending:
                job_id += 1
                jt, site = j["jt"], j["site"]
                tech, sched_start = None, None
                if random.random() < assign_p:
                    cands = [(t, p) for (t, p) in roster_by_skill.get(jt["skill"], []) if p >= jt["minp"]]
                    random.shuffle(cands)
                    best = None
                    for (t, p) in cands[:25]:
                        s = tl[t["id"]]
                        if s["count"] >= t["max"]:
                            continue
                        travel = int(km_between(s["loc"][0], s["loc"][1], site["lat"], site["lng"]) / 0.75) + 10  # ~45km/h
                        start = max(j["wstart"], s["cursor"] + timedelta(minutes=travel))
                        start = round_to(start + timedelta(minutes=14), 15)
                        if start + timedelta(minutes=j["dur"]) > s["end"] + timedelta(minutes=60):
                            continue
                        late = (start - j["wend"]).total_seconds() / 60
                        if late > (120 if days_ahead >= 0 else 60):
                            continue
                        score = late * 3 + (start - j["wstart"]).total_seconds() / 60 - p * 10
                        if best is None or score < best[0]:
                            best = (score, t, start)
                    if best:
                        _, tech, sched_start = best
                        s = tl[tech["id"]]
                        s["cursor"] = sched_start + timedelta(minutes=j["dur"])
                        s["count"] += 1
                        s["loc"] = (site["lat"], site["lng"])
                if tech is None and days_ahead <= 0:
                    # unplaceable demand: in history most never materialised; today keep a realistic backlog
                    keep = 0.12 if days_ahead < 0 else 0.4
                    if random.random() > keep:
                        job_id -= 1
                        continue
                sched_end = sched_start + timedelta(minutes=j["dur"]) if sched_start else None
                disp = random.choice(dispatchers_by_region[rid])["id"]
                created = j["created"]
                assigned_at = None
                if tech:
                    assigned_at = min(created + timedelta(minutes=random.randint(3, 180)), sched_start - timedelta(minutes=10))
                    assigned_at = max(assigned_at, created + timedelta(minutes=1))
                    if days_ahead >= 0 and assigned_at > sim_now:
                        assigned_at = sim_now - timedelta(minutes=random.randint(1, 30))
                        assigned_at = max(assigned_at, created + timedelta(minutes=1))

                status, actual_start, actual_end, ftf, rating, hold, comp_notes = "Unassigned", None, None, None, None, None, None
                if days_ahead < 0:
                    if tech is None:
                        status = "Cancelled"
                    else:
                        r = random.random()
                        if r < 0.045:
                            status = "Cancelled"
                        elif r < 0.052 and days_ahead > -10:
                            status = "OnHold"
                            hold = random.choice(HOLD_REASONS)
                            actual_start = sched_start + timedelta(minutes=random.randint(-10, 40))
                        else:
                            status = "Completed"
                            late_bias = random.random()
                            delay = random.randint(-15, 25) if late_bias < 0.94 else random.randint(30, 200)
                            actual_start = sched_start + timedelta(minutes=delay)
                            actual_end = actual_start + timedelta(minutes=int(j["dur"] * random.uniform(0.7, 1.6)))
                            ftf = 1 if random.random() < 0.86 else 0
                            rating = random.choices([5, 4, 3, 2, 1], [55, 28, 10, 4, 3])[0] if random.random() < 0.6 else None
                            comp_notes = random.choice(NOTE_TEXTS_TECH)
                elif days_ahead == 0:
                    if tech is None:
                        status = "Unassigned"
                    else:
                        ts = tech["id"]
                        if sched_end <= sim_now - timedelta(minutes=10):
                            r = random.random()
                            if r < 0.88:
                                status = "Completed"
                                actual_start = sched_start + timedelta(minutes=random.randint(-10, 30))
                                actual_end = min(actual_start + timedelta(minutes=int(j["dur"] * random.uniform(0.7, 1.3))), sim_now - timedelta(minutes=2))
                                ftf = 1 if random.random() < 0.85 else 0
                                comp_notes = random.choice(NOTE_TEXTS_TECH)
                            elif r < 0.94:
                                status = "OnSite"  # overrunning
                                actual_start = sched_start + timedelta(minutes=random.randint(0, 40))
                            else:
                                status = "OnHold"
                                hold = random.choice(HOLD_REASONS)
                                actual_start = sched_start + timedelta(minutes=random.randint(0, 20))
                        elif sched_start <= sim_now:
                            if random.random() < 0.8:
                                status = "OnSite"
                                actual_start = sched_start + timedelta(minutes=random.randint(0, 25))
                                if actual_start > sim_now:
                                    actual_start = sim_now - timedelta(minutes=3)
                            else:
                                status = "EnRoute"  # running late
                        elif sched_start <= sim_now + timedelta(minutes=60):
                            status = random.choice(["Dispatched", "EnRoute"])
                        else:
                            status = "Assigned"
                        # a technician can only be on site / en route at one job
                        if status in ("OnSite", "EnRoute"):
                            if ts in tech_today_state:
                                status = "Assigned" if sched_start > sim_now else "Dispatched"
                                actual_start = None if status != "OnSite" else actual_start
                            else:
                                tech_today_state[ts] = (status, site)
                else:
                    status = "Assigned" if tech else "Unassigned"

                job_number = f"WO-{d.strftime('%y%m%d')}-{job_id:06d}"
                desc = f"{random.choice(SUMMARY_PREFIX[jt['cat']])}. {jt['name']} at {site['name']}."
                source = random.choices(["Phone", "Portal", "Email", "IoTAlert", "Contract"], [35, 30, 10, 10, 15])[0]
                if jt["cat"] in ("Maintenance", "Inspection"):
                    source = "Contract"
                last_update = max(x for x in [created, assigned_at, actual_start, actual_end] if x)
                if days_ahead == 0 and status in ("Dispatched", "EnRoute"):
                    last_update = min(sim_now, max(last_update, sched_start - timedelta(minutes=60)))
                reschedule_count = 0

                # reschedule history (job moved from an earlier slot / other tech)
                if tech and random.random() < 0.08:
                    reschedule_count = random.choice([1, 1, 1, 2])
                    for k in range(reschedule_count):
                        from_tech = random.choice([tech["id"], random.choice(techs)["id"]])
                        from_start = sched_start - timedelta(days=random.randint(1, 4), hours=random.randint(-2, 2))
                        r_at = max(created + timedelta(minutes=5), min(from_start, sched_start) - timedelta(hours=random.randint(2, 20)))
                        if r_at > sim_now:
                            r_at = sim_now - timedelta(minutes=random.randint(30, 4320))
                            r_at = max(r_at, created + timedelta(minutes=5))
                        reason = random.choices(["CustomerRequest", "TechnicianUnavailable", "PartsDelay", "Overrun", "Weather", "SlaRebalance", "Other"],
                                                [35, 22, 14, 12, 5, 8, 4])[0]
                        resched_rows.append((len(resched_rows) + 1, job_id, from_tech, tech["id"], fmt(from_start), fmt(sched_start), reason,
                                             None if random.random() < 0.5 else "Moved per " + {"CustomerRequest": "operating dept. request", "TechnicianUnavailable": "tech availability",
                                                                                                "PartsDelay": "parts ETA", "Overrun": "previous job overrun", "Weather": "weather advisory",
                                                                                                "SlaRebalance": "SLA rebalancing", "Other": "dispatcher decision"}[reason],
                                             disp, fmt(r_at)))
                        add_event(job_id, "Rescheduled", r_at, tech=tech["id"], disp=disp, details=f"{fmt(from_start)} -> {fmt(sched_start)} ({reason})")

                job_rows.append((job_id, job_number, j["cust"]["id"], site["id"], j["asset"]["id"] if j["asset"] else None, jt["id"], rid,
                                 sla_id[(j["cust"]["tier"], j["prio"])], j["prio"], status, source,
                                 jt['name'], desc,
                                 fmt(created), fmt(j["wstart"]), fmt(j["wend"]), fmt(sched_start), fmt(sched_end), j["dur"],
                                 fmt(j["resp_due"]), fmt(j["res_due"]), tech["id"] if tech else None, disp if tech else None, fmt(assigned_at),
                                 fmt(actual_start), fmt(actual_end), hold, comp_notes, ftf, rating, reschedule_count, fmt(last_update)))

                # events
                add_event(job_id, "Created", created, to="Unassigned", details=f"Created via {source}")
                if tech:
                    add_event(job_id, "Assigned", assigned_at, "Unassigned", "Assigned", tech["id"], disp, f"Assigned to {tech['name']}")
                    flow = []
                    if status in ("Dispatched", "EnRoute", "OnSite", "OnHold", "Completed"):
                        flow.append(("Assigned", "Dispatched", (sched_start - timedelta(minutes=random.randint(45, 90)))))
                    if status in ("EnRoute", "OnSite", "OnHold", "Completed"):
                        flow.append(("Dispatched", "EnRoute", (actual_start or sched_start) - timedelta(minutes=random.randint(15, 40))))
                    if actual_start:
                        flow.append(("EnRoute", "OnSite", actual_start))
                    if status == "OnHold":
                        flow.append(("OnSite", "OnHold", actual_start + timedelta(minutes=random.randint(20, 60))))
                    if status == "Completed":
                        flow.append(("OnSite", "Completed", actual_end))
                    for (a, b, at) in flow:
                        at = max(at, assigned_at)
                        if days_ahead >= 0:
                            at = min(at, sim_now)
                        add_event(job_id, "StatusChanged", at, a, b, tech["id"], disp if b in ("Dispatched",) else None)
                    if status == "Cancelled":
                        add_event(job_id, "StatusChanged", sched_start - timedelta(hours=random.randint(1, 20)), "Assigned", "Cancelled", None, disp, "Cancelled by engineering")
                    if actual_start and actual_start > j["resp_due"]:
                        add_event(job_id, "SlaBreached", j["resp_due"], details="Response SLA breached")
                elif status == "Cancelled":
                    add_event(job_id, "StatusChanged", created + timedelta(hours=random.randint(1, 6)), "Unassigned", "Cancelled", None, disp, "Duplicate report")
                if days_ahead <= 0 and status not in ("Completed", "Cancelled") and j["res_due"] < sim_now:
                    add_event(job_id, "SlaBreached", j["res_due"], details="Resolution SLA breached")

                # notes
                if random.random() < 0.45:
                    for _ in range(random.randint(1, 3)):
                        when = created + timedelta(minutes=random.randint(5, 400))
                        if when > sim_now:
                            when = sim_now - timedelta(minutes=random.randint(1, 60))
                        if tech and actual_start and random.random() < 0.5:
                            note_rows.append((len(note_rows) + 1, job_id, "Technician", tech["name"], random.choice(NOTE_TEXTS_TECH), 0, fmt(max(actual_start, when) if actual_start <= sim_now else when)))
                        else:
                            dname = dispatchers[disp - 1]["name"]
                            note_rows.append((len(note_rows) + 1, job_id, "Dispatcher", dname, random.choice(NOTE_TEXTS_DISPATCH), 1, fmt(when)))
                if hold:
                    note_rows.append((len(note_rows) + 1, job_id, "Technician", tech["name"], f"Placed on hold: {hold}.", 0,
                                      fmt(min(actual_start + timedelta(minutes=30), sim_now))))

                # parts
                if status == "Completed" and jt["cat"] in ("Repair", "Emergency", "Install", "Maintenance") and random.random() < 0.55:
                    pool = parts_by_cat.get(skill_to_partcat[jt["skill_code"]], parts)
                    for p in random.sample(pool, k=min(len(pool), random.randint(1, 3))):
                        part_rows.append((len(part_rows) + 1, job_id, p["id"], random.randint(1, 4), p["price"], fmt(actual_end)))
                    add_event(job_id, "PartsUsed", actual_end, tech=tech["id"], details="Parts recorded on job")
        d += timedelta(days=1)

    cur.executemany("INSERT INTO Jobs VALUES (" + ",".join(["?"] * 32) + ")", job_rows)
    cur.executemany("INSERT INTO JobEvents VALUES (?,?,?,?,?,?,?,?,?)", event_rows)
    cur.executemany("INSERT INTO JobNotes VALUES (?,?,?,?,?,?,?)", note_rows)
    cur.executemany("INSERT INTO JobParts VALUES (?,?,?,?,?,?)", part_rows)
    cur.executemany("INSERT INTO Reschedules VALUES (?,?,?,?,?,?,?,?,?,?)", resched_rows)

    # ---------------- Technician live field status at sim-now ----------------
    today_roster = {t["id"]: (st, en) for (t, st, en) in on_shift[anchor]}
    for t in techs:
        if t["id"] in tech_today_state:
            state, site = tech_today_state[t["id"]]
            if state == "OnSite":
                lat, lng = site["lat"], site["lng"]
            else:
                lat, lng = jitter(site["lat"], site["lng"], 8)
            fs = state
        elif t["id"] in today_roster:
            fs = "OnBreak" if random.random() < 0.08 else "Available"
            dep = depots[t["depot"] - 1]
            lat, lng = jitter(dep["lat"], dep["lng"], 20)
        else:
            cur.execute("UPDATE Technicians SET FieldStatus='Offline' WHERE Id=?", (t["id"],))
            continue
        cur.execute("UPDATE Technicians SET FieldStatus=?, CurrentLatitude=?, CurrentLongitude=?, LastLocationAt=? WHERE Id=?",
                    (fs, lat, lng, fmt(sim_now - timedelta(minutes=random.randint(0, 9))), t["id"]))

    con.commit()
    con.execute("ANALYZE")
    con.commit()

    counts = {tbl: con.execute(f"SELECT COUNT(*) FROM {tbl}").fetchone()[0] for tbl in
              ["Regions", "Depots", "Dispatchers", "Skills", "Vehicles", "Technicians", "TechnicianSkills", "Shifts", "TimeOffRequests",
               "Customers", "Sites", "Assets", "SlaPolicies", "JobTypes", "Parts", "DepotInventory", "Jobs", "JobEvents", "JobNotes",
               "JobParts", "Reschedules"]}
    con.execute("VACUUM")
    con.close()
    print(f"Generated {args.out} (anchor {anchor}, simulated now {sim_now})")
    for k, v in counts.items():
        print(f"  {k:<18}{v:>9,}")


if __name__ == "__main__":
    main()
