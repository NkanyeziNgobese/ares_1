import paho.mqtt.client as mqtt
import time
import random

# --- Config ---
BROKER = "localhost"
PORT = 1883
TOPIC = "ares1/telemetry/realtime"

# Karoo Hazard Zone
SILL_TOP = 1225.0
SILL_BOTTOM = 1375.0

client = mqtt.Client(callback_api_version=mqtt.CallbackAPIVersion.VERSION2)
client.connect(BROKER, PORT)

depth = 1200.0 # Start just above the sill

print("Ares-1 Karoo Publisher: ACTIVE")

try:
    while depth < 1500:
        # Check if we are in the hard rock zone
        if SILL_TOP <= depth <= SILL_BOTTOM:
            rop = random.uniform(0.5, 1.5) # Hard rock: Very slow
            vibration = random.uniform(80.0, 100.0) # High chatter
            status = "HAZARD: DOLERITE SILL"
        else:
            rop = random.uniform(15.0, 25.0) # Normal shale drilling
            vibration = random.uniform(5.0, 15.0) # Smooth
            status = "DRILLING: SHALE"

        depth += (rop / 3600) # Increment depth
        
        payload = {
            "depth": round(depth, 3),
            "rop": round(rop, 2),
            "vibration": round(vibration, 2),
            "status": status
        }
        
        client.publish(TOPIC, str(payload))
        print(f"[{status}] Depth: {depth:.2f}m | ROP: {rop:.2f}m/hr")
        time.sleep(1)

except KeyboardInterrupt:
    client.disconnect()