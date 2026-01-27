import pandas as pd
import paho.mqtt.client as mqtt
import time
import json

# --- Config ---
BROKER = "localhost" # Using your local Docker Mosquitto
PORT = 1883
TOPIC = "ares1/telemetry/depth"
DATA_PATH = "volve_production_data.xlsx" # Ensure path matches your report

# Initialize MQTT (compat with paho-mqtt <2.0)
if hasattr(mqtt, "CallbackAPIVersion"):
    client = mqtt.Client(callback_api_version=mqtt.CallbackAPIVersion.VERSION2)
else:
    client = mqtt.Client()
client.connect(BROKER, PORT)

print(f"Ares-1: Reading Volve Data from {DATA_PATH}...")

# Load Data (Reading the first sheet for now)
try:
    df = pd.read_excel(DATA_PATH)
    # Ensure columns match Volve schema (adjust if names differ)
    # Typical Volve production columns: 'Measured Depth', 'Bit Position', etc.
    data_list = df.to_dict(orient='records')
except Exception as e:
    print(f"Error loading Excel: {e}")
    data_list = []

print("Ares-1: Starting Real-time Stream...")

for record in data_list:
    # Convert row to JSON string
    payload = json.dumps(record)
    client.publish(TOPIC, payload)
    
    print(f"Published to {TOPIC}: {payload[:50]}...") # Print preview
    time.sleep(1) # Stream at 1Hz (1 row per second)

client.disconnect()
