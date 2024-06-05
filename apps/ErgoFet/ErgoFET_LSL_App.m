function varargout = ErgoFET_LSL_App(varargin)
% ErgoFET_LSL_App MATLAB code for ErgoFET_LSL_App.fig
%      ErgoFET_LSL_App, by itself, creates a new ErgoFET_LSL_App or raises the existing
%      singleton*.
%
%      H = ErgoFET_LSL_App returns the handle to a new ErgoFET_LSL_App or the handle to
%      the existing singleton*.
%
%      ErgoFET_LSL_App('CALLBACK',hObject,eventData,handles,...) calls the local
%      function named CALLBACK in ErgoFET_LSL_App.M with the given input arguments.
%
%      ErgoFET_LSL_App('Property','Value',...) creates a new ErgoFET_LSL_App or raises the
%      existing singleton*.  Starting from the left, property value pairs are
%      applied to the GUI before ErgoFET_LSL_App_OpeningFcn gets called.  An
%      unrecognized property name or invalid value makes property application
%      stop.  All inputs are passed to ErgoFET_LSL_App_OpeningFcn via varargin.
%
%      *See GUI Options on GUIDE's Tools menu.  Choose "GUI allows only one
%      instance to run (singleton)".
%
% See also: GUIDE, GUIDATA, GUIHANDLES

% Edit the above text to modify the response to help ErgoFET_LSL_App

% Last Modified by GUIDE v2.5 10-Nov-2022 09:21:43

% Begin initialization code - DO NOT EDIT
gui_Singleton = 1;
gui_State = struct('gui_Name',       mfilename, ...
                   'gui_Singleton',  gui_Singleton, ...
                   'gui_OpeningFcn', @ErgoFET_LSL_App_OpeningFcn, ...
                   'gui_OutputFcn',  @ErgoFET_LSL_App_OutputFcn, ...
                   'gui_LayoutFcn',  [] , ...
                   'gui_Callback',   []);
if nargin && ischar(varargin{1})
    gui_State.gui_Callback = str2func(varargin{1});
end

if nargout
    [varargout{1:nargout}] = gui_mainfcn(gui_State, varargin{:});
else
    gui_mainfcn(gui_State, varargin{:});
end
% End initialization code - DO NOT EDIT


% --- Executes just before ErgoFET_LSL_App is made visible.
function ErgoFET_LSL_App_OpeningFcn(hObject, eventdata, handles, varargin)
% This function has no output args, see OutputFcn.
% hObject    handle to figure
% eventdata  reserved - to be defined in a future version of MATLAB
% handles    structure with handles and user data (see GUIDATA)
% varargin   command line arguments to ErgoFET_LSL_App (see VARARGIN)

% Choose default command line output for ErgoFET_LSL_App
handles.output = hObject;

% Update handles structure
guidata(hObject, handles);

% UIWAIT makes ErgoFET_LSL_App wait for user response (see UIRESUME)
% uiwait(handles.figure1);
global comPort;
comPort = 'COM5';


% --- Outputs from this function are returned to the command line.
function varargout = ErgoFET_LSL_App_OutputFcn(hObject, eventdata, handles) 
% varargout  cell array for returning output args (see VARARGOUT);
% hObject    handle to figure
% eventdata  reserved - to be defined in a future version of MATLAB
% handles    structure with handles and user data (see GUIDATA)

% Get default command line output from handles structure
varargout{1} = handles.output;


% --- Executes on button press in togglebutton1.
function togglebutton1_Callback(hObject, eventdata, handles)
% hObject    handle to togglebutton1 (see GCBO)
% eventdata  reserved - to be defined in a future version of MATLAB
% handles    structure with handles and user data (see GUIDATA)

% Hint: get(hObject,'Value') returns toggle state of togglebutton1

% instantiate the library %disp('Loading library...');
lib = lsl_loadlib();

% make a new stream outlet %disp('Creating a new streaminfo...');
info = lsl_streaminfo(lib,'ErgoFet','ForceGauge',1,100,'cf_float32','sdfwerr32432');
    % META data
    Channels  = info.desc().append_child('Channels');
    Channels.append_child_value('label', ['ErgoFet']);
    Channels.append_child_value('unit', 'N');
    Channels.append_child_value('precision', '0,5'); 

%          ch = Channels.append_child('Ch' + string(1));
%          ch.append_child_value('label', ['ErgoFet']);
%          ch.append_child_value('unit', 'N');
%          ch.append_child_value('precision', '0,5'); 

    hardware = info.desc().append_child('hardware');
    hardware.append_child_value('manufacturer', 'Hoggan Scientific')
    hardware.append_child_value('model', 'ERGOFET')
    hardware.append_child_value('serial', '27730W')
    hardware.append_child_value('config', '')
    hardware.append_child_value('location', 'VUB - AugmentX')
% 
    sync = info.desc().append_child('synchronization');
    sync.append_child_value('time_source', 'Mod0')
    sync.append_child_value('offset_mean', '0,0')
    sync.append_child_value('can_drop_samples', 'True')
    sync.append_child_value('inlet_processing_options', 'Clocksync')
    sync.append_child_value('outlet_processing_options', 'None')
    sync.append_child_value('outlet_drift_coeffificent', '1')
    sync.append_child_value('outlet_jitter_coeffificent', '0,2')  % Comma wordt een punt en omgekeerd

    % print if need to debug metadata:  
    % fprintf([info.as_xml() '\n']);

%disp('Opening an outlet...');
outlet = lsl_outlet(info);

%Specs Ergo FET : Range / Accuracy / Resolution sensor : ±1.5 kN / 1% / 0.5N / 100Hz

dataTypes = "uint8";
newtonPerPound=4.4482216153; 

% Byte 0: 255 or [1 1 1 1 1 1 1]
% Byte 1: [0 (0:ErgoFet) (1:high 0:low threshold) (0:n.u.) (0:n.u.) (0:n.u.) (0:n.u.) (0: compression)] 
% Byte 2: Hex -> Dec tientallen pounds
% Byte 3: Hex -> Dec tindes pounds 

try
    clear s
catch
end

if get(hObject,'Value') ==1
    global comPort
    try
        s = serialport(comPort,9600,"Timeout",300); 
        tic;
    catch 
        errordlg("Error: Unable to connect to the serial device. Verify device and port number.", 'Error message')
    end 
else
    %try
    %    lsl_destroy_outlet(outlet);
    %catch 
    %    msgbox('issue faced, when destroying the LSL outlet')
    %end
end

%figure(1); hold on; 
step =0;
while true
    if get(hObject,'Value') ==1
        try
            firstByte = read(s,1,dataTypes);
        catch 
            break
        end
        if firstByte ==255 
            data1 = read(s,1,dataTypes);
            data2 = read(s,1,dataTypes);
            data3 = read(s,1,dataTypes);
            signFact=mod(data1,2)*2-1;
            heavy = data2*10*newtonPerPound;
            light = data3*0.1*newtonPerPound;
            sig= signFact*(light+heavy); 
            outlet.push_sample(sig);
    
            step = step+1;  % plot(step,sig,".k") ; hold on

            set(handles.text4,'String','Status: Sending')
            set(handles.text3,'String',['Data packets: ', num2str(step*4/1000),' kB'])
            set(handles.text2,'String',['Elapsed: ', num2str(round(toc,2)),' s'])
        end
    else
        set(handles.text4,'String','Status: Not sending')
        set(handles.text2,'String',['Elapsed: ', num2str(round(toc,2)),' s'])

        clear s
       % lsl_destroy_outlet(outlet, 0);
        break
    end
end


function edit1_Callback(hObject, eventdata, handles)
% hObject    handle to edit1 (see GCBO)
% eventdata  reserved - to be defined in a future version of MATLAB
% handles    structure with handles and user data (see GUIDATA)

% Hints: get(hObject,'String') returns contents of edit1 as text
%        str2double(get(hObject,'String')) returns contents of edit1 as a double
global comPort
comPort = ['COM', get(hObject,'String')];

% --- Executes during object creation, after setting all properties.
function edit1_CreateFcn(hObject, eventdata, handles)
% hObject    handle to edit1 (see GCBO)
% eventdata  reserved - to be defined in a future version of MATLAB
% handles    empty - handles not created until after all CreateFcns called

% Hint: edit controls usually have a white background on Windows.
%       See ISPC and COMPUTER.
if ispc && isequal(get(hObject,'BackgroundColor'), get(0,'defaultUicontrolBackgroundColor'))
    set(hObject,'BackgroundColor','white');
end
