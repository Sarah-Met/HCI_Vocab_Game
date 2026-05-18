import cv2
import mediapipe as mp
import os
import json

base_folder = "dataset"
gesture_name = input("Gesture name: ").strip()

save_folder = os.path.join(base_folder, gesture_name)
os.makedirs(save_folder, exist_ok=True)

mp_hands = mp.solutions.hands
mp_draw = mp.solutions.drawing_utils

cap = cv2.VideoCapture(0)

sample_index = len(os.listdir(save_folder))
recording = False
points = []

with mp_hands.Hands(
    static_image_mode=False,
    max_num_hands=1,
    min_detection_confidence=0.7,
    min_tracking_confidence=0.7
) as hands:

    while True:
        ok, frame = cap.read()
        if not ok:
            break

        frame = cv2.flip(frame, 1)
        h, w, _ = frame.shape
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        result = hands.process(rgb)

        if result.multi_hand_landmarks:
            hand = result.multi_hand_landmarks[0]
            mp_draw.draw_landmarks(frame, hand, mp_hands.HAND_CONNECTIONS)

            x = int(hand.landmark[8].x * w)
            y = int(hand.landmark[8].y * h)

            cv2.circle(frame, (x, y), 8, (0, 255, 0), -1)

            if recording:
                points.append([x, y])

                for i in range(1, len(points)):
                    cv2.line(frame, tuple(points[i - 1]), tuple(points[i]), (255, 0, 0), 2)

        cv2.putText(frame, f"Gesture: {gesture_name}", (20, 40),
                    cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
        cv2.putText(frame, "r = start, s = save, c = clear, esc = exit", (20, 80),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 255), 2)

        cv2.imshow("Record Dataset", frame)
        key = cv2.waitKey(1) & 0xFF

        if key == 27:
            break
        elif key == ord('r'):
            recording = True
            points = []
            print("Recording started")
        elif key == ord('c'):
            recording = False
            points = []
            print("Cleared")
        elif key == ord('s'):
            recording = False
            if len(points) > 5:
                file_path = os.path.join(save_folder, f"sample_{sample_index}.json")
                with open(file_path, "w") as f:
                    json.dump(points, f)
                print("Saved:", file_path)
                sample_index += 1
                points = []
            else:
                print("Too few points")

cap.release()
cv2.destroyAllWindows()