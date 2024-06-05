"""
    Stream 3D and 6DOF data from QTM and pass it through an LSL outlet.
"""

import asyncio
from enum import Enum
import logging
import time
import copy

from pylsl import cf_double64, cf_float32, StreamInfo, StreamOutlet
import qtm
from qtm import QRTEvent

from qlsl.config import (
    Config,
    new_lsl_stream_info,
    parse_qtm_parameters,
    qtm_packet_to_lsl_sample,
)

LOG = logging.getLogger("qlsl")
QTM_DEFAULT_PORT = 22223
QTM_DEFAULT_VERSION = "1.19"

class State(Enum):
    INITIAL = 1
    WAITING = 2
    STREAMING = 3
    STOPPED = 4

class Link:
    def __init__(self, host, port, on_state_changed, on_error):
        self.host = host
        self.port = port
        self._on_state_changed = on_state_changed
        self._on_error = on_error

        self.state = State.INITIAL
        self.conn = None
        self.packet_count = 0
        self.start_time = 0
        self.stop_time = 0
        self.reset_stream_context()
    
    def reset_stream_context(self):
        self.config = Config()
        self.config_markers = Config()
        self.config_bodies = Config()
        self.config_skeletons = Config()
        self.receiver_queue = None
        self.receiver_task = None
        self.lsl_info = None
        self.lsl_info_markers = None
        self.lsl_info_bodies = None
        self.lsl_info_skeletons = [None, None]
        self.lsl_outlet = None
        self.lsl_outlet_markers = None
        self.lsl_outlet_bodies = None
        self.lsl_outlet_skeletons = [None, None]
    
    def set_state(self, state):
        prev_state = self.state
        self.state = state
        self.on_state_changed(self.state)
        return prev_state
    
    def is_streaming(self):
        return self.state == State.STREAMING
    
    def is_waiting(self):
        return self.state == State.WAITING

    def is_stopped(self):
        return self.state in [State.INITIAL, State.STOPPED]
    
    def elapsed_time(self):
        if self.start_time > 0:
            return time.time() - self.start_time
        return 0
    
    def final_time(self):
        if self.stop_time > self.start_time:
            return self.stop_time - self.start_time
        return 0
    
    def on_state_changed(self, new_state):
        if self._on_state_changed:
            self._on_state_changed(new_state)
    
    def on_error(self, msg):
        if self._on_error:
            self._on_error(msg)
    
    def on_event(self, event):
        start_events = [
            QRTEvent.EventRTfromFileStarted,
            QRTEvent.EventCalibrationStarted,
            QRTEvent.EventCaptureStarted,
            QRTEvent.EventConnected,
        ]
        stop_events = [
            QRTEvent.EventRTfromFileStopped,
            QRTEvent.EventCalibrationStopped,
            QRTEvent.EventCaptureStopped,
            QRTEvent.EventConnectionClosed,
        ]
        if self.state == State.WAITING:
            if event in start_events:
                asyncio.ensure_future(self.start_stream())
        elif self.state == State.STREAMING:
            if event in stop_events:
                asyncio.ensure_future(self.stop_stream())
        
    def on_disconnect(self, exc):
        if self.is_stopped(): return
        if self.conn:
            msg = "Disconnected from QTM"
            LOG.error(msg)
            self.err_disconnect(msg)
        if exc:
            LOG.debug("link: on_disconnect: {}".format(exc))

    def open_lsl_stream_outlet(self, content):
        self.lsl_info = new_lsl_stream_info(self.config, self.host, self.port, content)
        self.lsl_outlet = StreamOutlet(info=self.lsl_info, max_buffered=180)
    
    def err_disconnect(self, err_msg):
        asyncio.ensure_future(self.shutdown(err_msg))

    async def shutdown(self, err_msg=None):
        try:
            LOG.debug("link: shutdown enter")
            if self.state == State.STREAMING:
                await self.stop_stream()
            if self.conn and self.conn.has_transport():
                self.conn.disconnect()
            self.conn = None
        finally:
            self.set_state(State.STOPPED)
            if err_msg:
                self.on_error(err_msg)
            LOG.debug("link: shutdown exit")

    async def stop_stream(self):
        if self.conn and self.conn.has_transport():
            try:
                await self.conn.stream_frames_stop()
            except qtm.QRTCommandException as ex:
                LOG.error("QTM: stream_frames_stop exception: " + str(ex))
        if self.receiver_queue:
            self.receiver_queue.put_nowait(None)
            await self.receiver_task
        self.reset_stream_context()
        if self.state == State.STREAMING:
            LOG.info("Stream stopped")
            self.stop_time = time.time()
            self.set_state(State.WAITING)

    async def start_stream(self):
        try:
            packet = await self.conn.get_parameters(
                parameters=["general", "3d", "6d", "skeleton"],
            )
            config = parse_qtm_parameters(packet.decode("utf-8"))
            #config.output_settings()
            ################################################################################################
            # To print the configuration of the stream (Cameras, and settings, 3D, 6D, skeletons)
            # print(packet.decode("utf-8"))
            # print(config.the_3d)
            # print(config.the_6d)
            # print(config.the_skeletons)

            components_count = {"markers": config.marker_count(), "bodies": config.body_count(), "skeletons": config.skeleton_count()}
            if config.channel_count() == 0:
                msg = "Missing QTM data: markers {} rigid bodies {} and skeletons {}" \
                    .format(config.marker_count(), config.body_count(), config.skeleton_count())
                LOG.info(msg)
                self.err_disconnect("No 3D or 6DOF or skeletons data available from QTM")
                return
            self.config = config
            self.receiver_queue = asyncio.Queue()
            self.receiver_task = asyncio.ensure_future(self.stream_receiver())
            #Unique QRT receiver, containing all the data packets
            #Depending on the components, a new config and outlet is created for each of the 3 types (marker, body, skeleton)

            ###### Create a basic stream for debugging
            if False:
                channel_length=5
                rand_info = StreamInfo(name='Test Stream', type='random', channel_count=channel_length, channel_format=cf_double64, nominal_srate=10, source_id='myuid34234')
                rand_outlet=StreamOutlet(rand_info)
                t=0
                while t<1000:
                    rand_samp=[float(j) for j in range(channel_length)]
                    print(rand_samp)
                    rand_outlet.push_sample(rand_samp)
                    time.sleep(0.1)
                    t+=1
            # print('The amount of components (from config) in order are : ' + str(components_count))
            components_to_stream = ["skeleton"]#"3d", "6deuler",
            if components_count["markers"]>0 and "3d" in components_to_stream:
                print('Information for {0} markers received, awaiting {1} channels'.format(components_count["markers"], components_count["markers"]*3))
                self.config_markers = copy.deepcopy(config)
                self.config_bodies.the_6d = {}
                self.config_skeletons.the_skeletons = {}
                self.lsl_info_markers = new_lsl_stream_info(self.config_markers, self.host, self.port, content="markers")
                self.lsl_outlet_markers = StreamOutlet(info=self.lsl_info_markers, max_buffered=180)
                # self.open_lsl_stream_outlet(content="markers")
            if components_count["bodies"]>0 and "6deuler" in components_to_stream:
                print('Information for {0} rigid bodies (6DOF) received, awaiting {1} channels'.format(components_count["bodies"], components_count["bodies"]*6))
                self.config_bodies = copy.deepcopy(config)
                self.config_bodies.the_3d = {}
                self.config_skeletons.the_skeletons = {}
                self.lsl_info_bodies = new_lsl_stream_info(self.config_bodies, self.host, self.por, content="bodies")
                self.lsl_outlet_bodies = StreamOutlet(info=self.lsl_info_bodies, max_buffered=180)
                # self.open_lsl_stream_outlet(content="bodies")
            if components_count["skeletons"]>0 and "skeleton" in components_to_stream:
                print('Information for {0} skeletons received, awaiting {1} channels par stream'.format(components_count["skeletons"], components_count["skeletons"]*168))
                self.config_skeletons = copy.deepcopy(config)
                self.config_skeletons.the_3d = {}
                self.config_skeletons.the_6d = {}
                skeleton_names=list(self.config_skeletons.skeleton().keys())
                self.lsl_info_skeletons = []
                self.lsl_outlet_skeletons = []
                for i in range(components_count["skeletons"]):
                    skeleton_name = skeleton_names[i]
                    self.lsl_info_skeletons.append(new_lsl_stream_info(self.config_skeletons, self.host, self.port, content="skeletons", name=skeleton_name))
                    self.lsl_outlet_skeletons.append(StreamOutlet(info=self.lsl_info_skeletons[i], max_buffered=180))
                # self.open_lsl_stream_outlet(content="skeletons")
            
            # self.open_lsl_stream_outlet()
            
            await self.conn.stream_frames(
                components= components_to_stream,
                on_packet=self.receiver_queue.put_nowait,)

            log_info_builder = "Stream started with "
            if "3d" in components_to_stream:
                log_info_builder += " {0} marker(s),".format(config.marker_count())
            if "6deuler" in components_to_stream:
                log_info_builder += " {0} bod(y/ies),".format(config.body_count())
            if "skeleton" in components_to_stream:
                log_info_builder += " {0} skeleton(s)".format(config.skeleton_count())

            LOG.info(log_info_builder)
            self.packet_count = 0
            self.start_time = time.time()
            self.set_state(State.STREAMING)
        except asyncio.CancelledError:
            raise
        except qtm.QRTCommandException as ex:
            LOG.error("QTM: stream_frames exception: " + str(ex))
            self.err_disconnect("QTM error: {}".format(ex))
        except Exception as ex:
            LOG.error("link: start_stream exception: " + repr(ex))
            self.err_disconnect("An internal error occurred. See log messages for details.")
            raise ex


    async def stream_receiver(self):
        try:
            LOG.debug("link: stream_receiver enter")
            while True:
                packet = await self.receiver_queue.get()
                if packet is None:
                    break
                sample = qtm_packet_to_lsl_sample(packet)

                ####################################################################################################
                # To print the contents of the sample
                # print('Sample is : \t', str(sample))
                # print('Config channel counts (from config) : {0}, {1}, {2}'.format(self.config_markers.channel_count(), self.config_bodies.channel_count(), self.config_skeletons.channel_count()))

                if len(sample[0])*3 != self.config_markers.channel_count() and len(sample[0]) != 0:
                    msg = "Stream canceled: markers sample length {} != channel count {}".format(len(sample[0]), self.config_markers.channel_count())
                    LOG.error(msg)
                    self.err_disconnect(("Stream canceled: QTM stream data inconsistent with LSL metadata"))
                elif len(sample[1])*6 != self.config_bodies.channel_count() and len(sample[1]) != 0:
                    msg = "Stream canceled: bodies sample length {} != channel count {}".format(len(sample[1]), self.config_bodies.channel_count())
                    LOG.error(msg)
                    self.err_disconnect(("Stream canceled: QTM stream data inconsistent with LSL metadata"))
                
                # It seems that partial skeletons (only upper body only has 16 segments, thus the channel count will change!!!)
                elif len(sample[2])*7*24 != self.config_skeletons.channel_count() and len(sample[2]) != 0:
                    msg = "Stream canceled: skeletons sample length {} != channel count {}".format(len(sample[2]), self.config_skeletons.channel_count())
                    LOG.error(msg)
                    self.err_disconnect(("Stream canceled: QTM stream data inconsistent with LSL metadata"))
                
                else:
                    self.packet_count += 1
                    if len(sample[0])>0:
                        sample_markers = sample[0][0]
                        for m in sample[0][1:]:
                            sample_markers += m
                        self.lsl_outlet_markers.push_sample(sample_markers)
                    if len(sample[1])>0:
                        sample_bodies = sample[1][0]
                        for b in sample[1][1:]:
                            sample_bodies += b
                        self.lsl_outlet_bodies.push_sample(sample_bodies)
                    if len(sample[2])>0:
                        # print('Skeleton Sample is : '+ str(sample[2][0]))
                        sample_skeletons = sample[2][0]
                        # default_sample = [float(j) for j in range(168)]
                        for i in range(len(sample[2])):
                            #self.lsl_outlet_skeletons[i].push_sample(default_sample)
                            self.lsl_outlet_skeletons[i].push_sample(sample[2][i])

        except asyncio.CancelledError:
            raise
        except Exception as ex:
            LOG.error("link: stream_receiver exception: " + repr(ex))
            self.err_disconnect("An internal error occurred. See log messages for details.")
            raise
        finally:
            LOG.debug("link: stream_receiver exit")

class LinkError(Exception):
    pass

async def init(
    qtm_host,
    qtm_port=QTM_DEFAULT_PORT,
    qtm_version=QTM_DEFAULT_VERSION,
    on_state_changed=None,
    on_error=None
):
    LOG.debug("link: init enter")
    link = Link(qtm_host, qtm_port, on_state_changed, on_error)
    try:
        link.conn = await qtm.connect(
            host=qtm_host,
            port=qtm_port,
            version=qtm_version,
            on_event=link.on_event,
            on_disconnect=link.on_disconnect,
        )
        if link.conn is None:
            msg = ("Failed to connect to QTM "
                "on '{}:{}' with protocol version '{}'") \
                .format(qtm_host, qtm_port, qtm_version)
            LOG.error(msg)
            raise LinkError(msg)
        try:
            link.set_state(State.WAITING)
            await link.conn.get_state()
        except qtm.QRTCommandException as ex:
            LOG.error("QTM: get_state exception: " + str(ex))
            raise LinkError("QTM error: {}".format(ex))
    except:
        await link.shutdown()
        raise
    finally:
        LOG.debug("link: init exit")
    return link
