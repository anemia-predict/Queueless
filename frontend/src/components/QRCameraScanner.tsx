import React, { useEffect, useRef, useState } from 'react';
import { Html5Qrcode, Html5QrcodeCameraScanConfig, Html5QrcodeSupportedFormats } from 'html5-qrcode';
import {
  Camera,
  CameraOff,
  CheckCircle2,
  AlertCircle,
  Building2,
  Stethoscope,
  Sparkles,
  Upload,
  Smartphone,
  HelpCircle,
  X,
  Keyboard,
  ArrowRight,
  Loader2,
  ExternalLink,
} from 'lucide-react';

interface QRCameraScannerProps {
  onScan: (decodedText: string, stage: 'AUTO' | 'BUILDING' | 'ROOM') => void;
  defaultStage?: 'AUTO' | 'BUILDING' | 'ROOM';
  allowedStages?: ('BUILDING' | 'ROOM')[];
  title?: string;
  onClose?: () => void;
}

export const QRCameraScanner: React.FC<QRCameraScannerProps> = ({
  onScan,
  defaultStage = 'AUTO',
  allowedStages = ['BUILDING', 'ROOM'],
  title = 'Scan Patient E-Token QR',
  onClose,
}) => {
  const [selectedStage, setSelectedStage] = useState<'AUTO' | 'BUILDING' | 'ROOM'>(defaultStage);
  const [isScanning, setIsScanning] = useState(false);
  const [isStarting, setIsStarting] = useState(true);
  const [cameraError, setCameraError] = useState<string>('');
  const [cameras, setCameras] = useState<{ id: string; label: string }[]>([]);
  const [selectedCameraId, setSelectedCameraId] = useState<string>('');
  const [lastScanned, setLastScanned] = useState<string | null>(null);
  const [isProcessingFile, setIsProcessingFile] = useState(false);
  const [manualTokenInput, setManualTokenInput] = useState('');

  const scannerRef = useRef<Html5Qrcode | null>(null);
  const scanLockRef = useRef(false);
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const photoCaptureInputRef = useRef<HTMLInputElement | null>(null);
  const containerId = 'qr-camera-stream-container';

  const isMediaSupported = typeof navigator !== 'undefined' &&
    Boolean(navigator.mediaDevices && typeof navigator.mediaDevices.getUserMedia === 'function');
  const [cameraStreamingSupported, setCameraStreamingSupported] = useState<boolean>(isMediaSupported);

  const getOrCreateScanner = () => {
    if (!scannerRef.current) {
      scannerRef.current = new Html5Qrcode(containerId, {
        formatsToSupport: [Html5QrcodeSupportedFormats.QR_CODE],
        verbose: false,
        experimentalFeatures: {
          useBarCodeDetectorIfSupported: true,
        },
      });
    }
    return scannerRef.current;
  };

  const playSuccessChime = () => {
    try {
      const audioCtx = new (window.AudioContext || (window as any).webkitAudioContext)();
      const osc = audioCtx.createOscillator();
      const gain = audioCtx.createGain();
      osc.connect(gain);
      gain.connect(audioCtx.destination);
      osc.frequency.value = 880;
      gain.gain.value = 0.15;
      osc.start();
      osc.stop(audioCtx.currentTime + 0.14);
    } catch (_) {}
  };

  const startCamera = async (cameraId?: string) => {
    setCameraError('');
    setLastScanned(null);
    setIsStarting(true);
    scanLockRef.current = false;

    try {
      const scanner = getOrCreateScanner();
      if (scanner.isScanning) {
        await scanner.stop();
      }

      const targetCam = cameraId || selectedCameraId;
      const config: Html5QrcodeCameraScanConfig = {
        fps: 15,
        qrbox: (viewfinderWidth, viewfinderHeight) => {
          const edge = Math.floor(Math.min(viewfinderWidth, viewfinderHeight) * 0.72);
          return { width: Math.max(edge, 180), height: Math.max(edge, 180) };
        },
        aspectRatio: 1.0,
      };

      // Prefer rear/environment camera on mobile; default to first camera or webcam
      const cameraParam = targetCam ? targetCam : { facingMode: 'environment' };

      await scanner.start(
        cameraParam,
        config,
        (decodedText) => {
          if (scanLockRef.current) return;
          scanLockRef.current = true;
          playSuccessChime();
          setLastScanned(decodedText);
          onScan(decodedText, selectedStage);

          setTimeout(() => {
            scanLockRef.current = false;
          }, 2500);
        },
        () => {
          // ignore individual frame evaluation misses
        }
      );

      setIsScanning(true);
      setCameraStreamingSupported(true);
    } catch (err: any) {
      console.warn('Failed to start camera:', err);
      const errMsg = typeof err === 'string' ? err : err?.message || 'Unable to access camera.';
      if (errMsg.toLowerCase().includes('not supported') || errMsg.toLowerCase().includes('streaming')) {
        setCameraStreamingSupported(false);
        const isHttp = typeof window !== 'undefined' && window.location.protocol === 'http:' && window.location.hostname !== 'localhost';
        if (isHttp) {
          setCameraError(`Live camera on mobile HTTP requires Chrome secure flag: Open chrome://flags/#unsafely-treat-insecure-origin-as-secure, add http://${window.location.host}, tap Enabled & Relaunch. Or open on your laptop via http://localhost:5173.`);
        } else {
          setCameraError('Camera streaming not supported by browser. Ensure camera permissions are granted in browser settings.');
        }
      } else if (errMsg.toLowerCase().includes('notallowed') || errMsg.toLowerCase().includes('permission')) {
        setCameraError('Camera permission denied. Please click the camera icon in your browser address bar and choose "Allow".');
      } else {
        setCameraError(errMsg);
      }
      setIsScanning(false);
    } finally {
      setIsStarting(false);
    }
  };

  const stopCamera = async () => {
    if (scannerRef.current) {
      try {
        if (scannerRef.current.isScanning) {
          await scannerRef.current.stop();
        }
      } catch (err) {
        console.warn('Error stopping scanner:', err);
      }
    }
    setIsScanning(false);
    setIsStarting(false);
  };

  // Auto-launch live camera immediately on mount
  useEffect(() => {
    let mounted = true;

    const init = async () => {
      try {
        if (isMediaSupported) {
          const devices = await Html5Qrcode.getCameras().catch(() => []);
          if (mounted && devices && devices.length > 0) {
            setCameras(devices.map((d) => ({ id: d.id, label: d.label || `Camera ${d.id.substring(0, 5)}` })));
            const backCam = devices.find(d => {
              const lbl = (d.label || '').toLowerCase();
              return lbl.includes('back') || lbl.includes('rear') || lbl.includes('environment');
            });
            const camId = backCam ? backCam.id : devices[0].id;
            setSelectedCameraId(camId);
            if (mounted) {
              await startCamera(camId);
            }
            return;
          }
        }
        if (mounted) {
          await startCamera();
        }
      } catch (e) {
        console.warn('Auto-init camera error:', e);
      }
    };

    // Small delay to ensure container element is mounted in DOM
    const timer = setTimeout(() => {
      init();
    }, 150);

    return () => {
      mounted = false;
      clearTimeout(timer);
      stopCamera();
    };
  }, []);

  const handleCameraChange = async (e: React.ChangeEvent<HTMLSelectElement>) => {
    const newId = e.target.value;
    setSelectedCameraId(newId);
    await stopCamera();
    await startCamera(newId);
  };

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setCameraError('');
    setLastScanned(null);
    setIsProcessingFile(true);

    try {
      if (isScanning) {
        await stopCamera();
      }
      const scanner = getOrCreateScanner();
      const decodedText = await scanner.scanFile(file, true);
      playSuccessChime();
      setLastScanned(decodedText);
      onScan(decodedText, selectedStage);
    } catch (err: any) {
      console.warn('File scan error:', err);
      setCameraError('Could not detect a clear QR code in this image. Please center the QR code and try again.');
    } finally {
      setIsProcessingFile(false);
      if (e.target) {
        e.target.value = '';
      }
    }
  };

  const handleManualTokenSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const token = manualTokenInput.trim();
    if (!token) return;
    setCameraError('');
    playSuccessChime();
    setLastScanned(token);
    onScan(token, selectedStage);
    setManualTokenInput('');
  };

  return (
    <div className="space-y-4">
      {/* Hidden native camera capture & file upload inputs */}
      <input
        ref={photoCaptureInputRef}
        type="file"
        accept="image/*"
        capture="environment"
        onChange={handleFileUpload}
        className="hidden"
      />
      <input
        ref={fileInputRef}
        type="file"
        accept="image/*"
        onChange={handleFileUpload}
        className="hidden"
      />

      {/* Stage Selection Bar */}
      <div>
        <label className="block text-[11px] font-bold text-slate-500 uppercase tracking-wider mb-1.5">
          Scan Check-in Target Point
        </label>
        <div className="grid grid-cols-3 gap-2">
          <button
            type="button"
            onClick={() => setSelectedStage('AUTO')}
            className={`py-2 px-3 rounded-xl border text-xs font-bold transition flex items-center justify-center space-x-1.5 ${
              selectedStage === 'AUTO'
                ? 'bg-teal-600 border-teal-600 text-white shadow-sm'
                : 'bg-white border-slate-200 text-slate-700 hover:bg-slate-50'
            }`}
          >
            <Sparkles className="w-3.5 h-3.5" />
            <span>Smart Auto-Stage</span>
          </button>

          {allowedStages.includes('BUILDING') && (
            <button
              type="button"
              onClick={() => setSelectedStage('BUILDING')}
              className={`py-2 px-3 rounded-xl border text-xs font-bold transition flex items-center justify-center space-x-1.5 ${
                selectedStage === 'BUILDING'
                  ? 'bg-emerald-600 border-emerald-600 text-white shadow-sm'
                  : 'bg-white border-slate-200 text-slate-700 hover:bg-slate-50'
              }`}
            >
              <Building2 className="w-3.5 h-3.5" />
              <span>1. Building Entry</span>
            </button>
          )}

          {allowedStages.includes('ROOM') && (
            <button
              type="button"
              onClick={() => setSelectedStage('ROOM')}
              className={`py-2 px-3 rounded-xl border text-xs font-bold transition flex items-center justify-center space-x-1.5 ${
                selectedStage === 'ROOM'
                  ? 'bg-indigo-600 border-indigo-600 text-white shadow-sm'
                  : 'bg-white border-slate-200 text-slate-700 hover:bg-slate-50'
              }`}
            >
              <Stethoscope className="w-3.5 h-3.5" />
              <span>2. Doctor Room</span>
            </button>
          )}
        </div>
        <p className="text-[11px] text-slate-400 mt-1">
          {selectedStage === 'AUTO' && '🔄 Smart Mode: Automatically verifies building entry first, or admits into room if already in building.'}
          {selectedStage === 'BUILDING' && '🏢 Building Mode: Verifies patient presence at hospital main gate / reception check-in.'}
          {selectedStage === 'ROOM' && '🩺 Doctor Room Mode: Admits called patient into consultation chamber within the 2-min window.'}
        </p>
      </div>

      {/* Main Viewport / Camera Box */}
      <div className="relative bg-slate-950 rounded-2xl overflow-hidden border border-slate-800 shadow-inner flex flex-col items-center justify-center min-h-[320px]">
        {/* Close Button on Viewport */}
        {onClose && (
          <button
            type="button"
            onClick={async () => {
              await stopCamera();
              onClose();
            }}
            aria-label="Close scanner"
            title="Close scanner"
            className="absolute top-3 right-3 z-30 p-2 rounded-xl bg-slate-900/80 hover:bg-rose-600 text-white shadow-lg backdrop-blur-xs transition flex items-center justify-center cursor-pointer group"
          >
            <X className="w-4 h-4 group-hover:scale-110 transition-transform stroke-[2.5]" />
          </button>
        )}

        {/* DOM container where html5-qrcode mounts the <video> element */}
        <div
          id={containerId}
          className="w-full flex items-center justify-center overflow-hidden [&_video]:w-full [&_video]:max-h-[340px] [&_video]:object-cover [&_video]:rounded-2xl"
        />

        {/* Live Scanning Laser Overlay */}
        {isScanning && (
          <div className="absolute inset-0 pointer-events-none flex flex-col items-center justify-center p-4">
            <div className="relative w-64 h-64 border-2 border-teal-400/80 rounded-2xl shadow-[0_0_24px_rgba(45,212,191,0.25)] flex items-center justify-center">
              <div className="absolute top-0 left-0 w-5 h-5 border-t-4 border-l-4 border-teal-400 -mt-1 -ml-1 rounded-tl" />
              <div className="absolute top-0 right-0 w-5 h-5 border-t-4 border-r-4 border-teal-400 -mt-1 -mr-1 rounded-tr" />
              <div className="absolute bottom-0 left-0 w-5 h-5 border-b-4 border-l-4 border-teal-400 -mb-1 -ml-1 rounded-bl" />
              <div className="absolute bottom-0 right-0 w-5 h-5 border-b-4 border-r-4 border-teal-400 -mb-1 -mr-1 rounded-br" />
              <div className="absolute left-2 right-2 h-0.5 bg-gradient-to-r from-transparent via-teal-300 to-transparent shadow-[0_0_8px_#2dd4bf] animate-[bounce_2s_infinite]" />
            </div>
            <div className="mt-3 px-3.5 py-1.5 rounded-full bg-slate-900/90 backdrop-blur-sm text-[11px] text-teal-300 font-semibold border border-teal-500/30 flex items-center space-x-1.5 shadow-lg">
              <Smartphone className="w-3.5 h-3.5 text-teal-400" />
              <span>Point live camera at patient phone QR • Max brightness</span>
            </div>
          </div>
        )}

        {/* Loading / Starting Camera Overlay */}
        {isStarting && !isScanning && (
          <div className="absolute inset-0 bg-slate-950/90 flex flex-col items-center justify-center p-6 text-center space-y-3 z-10">
            <Loader2 className="w-8 h-8 text-teal-400 animate-spin" />
            <p className="text-xs font-semibold text-slate-200">Starting Live Camera Scanner...</p>
            <p className="text-[11px] text-slate-400 max-w-xs">Connecting to device camera stream. Please allow camera permissions if prompted.</p>
          </div>
        )}

        {/* Processing Image Overlay */}
        {isProcessingFile && (
          <div className="absolute inset-0 bg-slate-950/85 backdrop-blur-xs flex flex-col items-center justify-center p-6 text-center space-y-3 z-20">
            <Loader2 className="w-8 h-8 text-teal-400 animate-spin" />
            <p className="text-sm font-semibold text-white">Analyzing QR code photo...</p>
          </div>
        )}

        {/* Idle/Failed State Controls */}
        {!isScanning && !isStarting && !isProcessingFile && (
          <div className="p-6 text-center text-slate-300 space-y-4 max-w-sm z-10">
            <div className="w-12 h-12 mx-auto rounded-2xl bg-teal-500/10 border border-teal-500/20 flex items-center justify-center text-teal-400">
              <Camera className="w-6 h-6" />
            </div>
            <div>
              <p className="text-sm font-bold text-white">Live Camera Scanner</p>
              <p className="text-xs text-slate-400 mt-1">
                Activate the live camera stream to scan digital QR tokens in real-time.
              </p>
            </div>
            <div className="flex flex-col sm:flex-row gap-2 justify-center pt-1">
              <button
                type="button"
                onClick={() => startCamera()}
                className="px-5 py-2.5 rounded-xl bg-teal-500 hover:bg-teal-400 text-slate-950 font-bold text-xs shadow-lg shadow-teal-500/20 transition cursor-pointer flex items-center justify-center space-x-1.5"
              >
                <Camera className="w-4 h-4" />
                <span>Start Live Camera</span>
              </button>
              <button
                type="button"
                onClick={() => photoCaptureInputRef.current?.click()}
                className="px-4 py-2.5 rounded-xl bg-slate-800 hover:bg-slate-700 text-slate-200 font-semibold text-xs border border-slate-700 transition cursor-pointer flex items-center justify-center space-x-1.5"
              >
                <Upload className="w-3.5 h-3.5 text-slate-400" />
                <span>Take Photo / Upload</span>
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Camera Controls & Status Bar */}
      <div className="flex flex-col sm:flex-row items-center justify-between gap-3 text-xs">
        {cameras.length > 1 && (
          <div className="flex items-center space-x-2 w-full sm:w-auto">
            <span className="text-slate-500 font-medium shrink-0">Switch Camera:</span>
            <select
              value={selectedCameraId}
              onChange={handleCameraChange}
              className="p-1.5 rounded-lg border border-slate-200 text-slate-700 text-xs bg-white focus:outline-none focus:border-teal-500 w-full"
            >
              {cameras.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.label}
                </option>
              ))}
            </select>
          </div>
        )}

        <div className="flex items-center space-x-2 w-full sm:w-auto justify-end">
          {isScanning ? (
            <button
              type="button"
              onClick={stopCamera}
              className="px-4 py-2 rounded-xl border border-rose-200 bg-rose-50 hover:bg-rose-100 text-rose-700 font-semibold flex items-center space-x-1.5 transition cursor-pointer"
            >
              <CameraOff className="w-3.5 h-3.5" />
              <span>Turn Off Camera</span>
            </button>
          ) : (
            <button
              type="button"
              onClick={() => startCamera()}
              className="px-4 py-2 rounded-xl bg-teal-600 hover:bg-teal-700 text-white font-semibold flex items-center space-x-1.5 transition shadow-sm cursor-pointer"
            >
              <Camera className="w-3.5 h-3.5" />
              <span>Turn On Live Camera</span>
            </button>
          )}
        </div>
      </div>

      {/* Quick Manual Token Entry Fallback */}
      <div className="bg-slate-50 border border-slate-200 rounded-2xl p-3.5">
        <form onSubmit={handleManualTokenSubmit} className="space-y-2">
          <div className="flex items-center space-x-1.5 text-xs font-bold text-slate-700">
            <Keyboard className="w-4 h-4 text-teal-600" />
            <span>Direct Token Number Entry</span>
          </div>
          <div className="flex gap-2">
            <input
              type="text"
              placeholder="e.g. 1, 2, #1 or paste QR payload"
              value={manualTokenInput}
              onChange={(e) => setManualTokenInput(e.target.value)}
              className="flex-1 px-3 py-2 text-xs rounded-xl border border-slate-200 bg-white focus:outline-none focus:border-teal-500 font-medium"
            />
            <button
              type="submit"
              disabled={!manualTokenInput.trim()}
              className="px-4 py-2 bg-teal-600 hover:bg-teal-700 disabled:opacity-40 text-white font-bold text-xs rounded-xl shadow-xs transition flex items-center space-x-1 cursor-pointer"
            >
              <span>Verify & Check-In</span>
              <ArrowRight className="w-3.5 h-3.5" />
            </button>
          </div>
        </form>
      </div>

      {cameraError && (
        <div className="p-3 bg-amber-50 border border-amber-200 rounded-xl text-amber-900 text-xs flex items-start space-x-2.5">
          <AlertCircle className="w-4 h-4 shrink-0 text-amber-600 mt-0.5" />
          <div className="space-y-1">
            <span className="font-semibold block">{cameraError}</span>
          </div>
        </div>
      )}

      {lastScanned && (
        <div className="p-3 bg-emerald-50 border border-emerald-200 rounded-xl text-emerald-900 text-xs flex items-center space-x-2 animate-pulse">
          <CheckCircle2 className="w-4 h-4 shrink-0 text-emerald-600" />
          <span className="truncate font-semibold">Verified Token / QR: {lastScanned}</span>
        </div>
      )}

      {/* Bottom Close Button */}
      {onClose && (
        <div className="pt-2 border-t border-slate-100 flex justify-end">
          <button
            type="button"
            onClick={async () => {
              await stopCamera();
              onClose();
            }}
            className="w-full sm:w-auto px-5 py-2.5 rounded-xl bg-slate-800 hover:bg-slate-900 text-white font-bold text-xs flex items-center justify-center space-x-2 transition shadow-sm cursor-pointer"
          >
            <X className="w-4 h-4 stroke-[2.5]" />
            <span>Close Scanner</span>
          </button>
        </div>
      )}
    </div>
  );
};