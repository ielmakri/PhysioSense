"""
    Handle QTM parameters, LSL metadata, and conversion from QTM data to LSL data.
"""

from distutils.command.config import config
import logging
from turtle import position
import xml.etree.ElementTree as ET
import math
import numpy as np
import xmltodict
import json

from pylsl import cf_double64, cf_float32, StreamInfo
from qtm.packet import QRTComponentType

LOG = logging.getLogger("qlsl")
LOG.setLevel(logging.DEBUG)

class Config:
    def __init__(self):
        self.general = {}
        self.the_3d = {}
        self.the_6d = {}
        self.the_skeletons = {}
    
    def markers(self):
        try:
            return self.the_3d["markers"]
        except KeyError:
            return []
    
    def marker_count(self):
        return len(self.markers())
    
    def bodies(self):
        try:
            return self.the_6d["bodies"]
        except KeyError:
            return []
    
    def body_count(self):
        return len(self.bodies())
    
    def skeleton(self):
        try:
            return self.the_skeletons
        except KeyError:
            return {}

    def skeleton_count(self):
        return len(self.skeleton().keys())

    def cameras(self):
        try:
            return self.general["cameras"]
        except KeyError:
            return []
    
    def camera_count(self):
        return len(self.cameras())

    def channel_count(self):
        return 3*self.marker_count() + 6*self.body_count() + 7*24*self.skeleton_count()#Amount of stream depends on markers on the body (might have to be changed for partial body ergonomics)
        # Might be intereting to remove the markers if we only use bodies and skeletons

    def output_settings(self):
        
        client = ET.Element("Qualisys QRT Streams")
        client.attrib['Name'] = 'Qualisys QRT Streams'
        client.attrib['Ip'] = '127.0.0.1'
        client.attrib['Port'] = '22223'
        streamers = ET.Element('Streamers')
        #markers = ET.Element('Markers')
        #bodies = ET.Element('Bodies')
        for key, values in self.skeleton().items():
            
            skeleton = ET.Element('Skeleton')
            skeleton.attrib['Name'] = key
            skeleton.attrib['Type'] = 'fullbody skeleton 6DOF'
            skeleton.attrib['NbChannel'] = '168'
            skeleton.attrib['ChFormat'] = 'cfloat32'
            skeleton.attrib['SRate'] = str(self.general['frequency'])
            for seg_key, seg_val in values.items():
                segment = ET.Element('Segment')
                segment.attrib['Name'] = seg_key
                segment.attrib['Type'] = 'skeleton segment (Bone)'
                segment.attrib['NbChannel'] = '7'
                segment.attrib['ChFormat'] = 'cfloat32'
                segment.attrib['SRate'] = str(self.general['frequency'])
                for p_key, p_val in seg_val.items():
                    chan = ET.Element('Segment coordinate')
                    chan.attrib['Label'] = key + seg_key + p_key
                    if p_key[0] == 'q':
                        chan.attrib['Unit'] = '/'
                    else:
                        chan.attrib['Unit'] = 'mm'
                    chan.attrib['Precision'] = '3'
                    segment.append(chan)
                skeleton.append(segment)
            streamers.append(skeleton)
        client.append(streamers)
        settings_tree = ET.ElementTree(client)
'''        with open("settings.xml", 'wb') as xml_file:
            settings_tree.write(xml_file)
        with open("settings.xml") as xml_file:
            data_dict = xmltodict.parse(xml_file.read())
        json_data = json.dumps(data_dict)
        with open("settings.json", "w") as json_file:
            json_file.write(json_data)'''


def parse_qtm_parameters(xml_string):#XML format can be found here: https://docs.qualisys.com/qtm-rt-protocol/#xml-packet
    xml = ET.fromstring(xml_string)
    config = Config()
    xml_general = xml.find("./General")
    if xml_general:
        config.general = parse_qtm_parameters_general(xml_general)
    xml_3d = xml.find("./The_3D")
    if xml_3d:
        config.the_3d = parse_qtm_parameters_3d(xml_3d)
    xml_6d = xml.find("./The_6D")
    if xml_6d:
        config.the_6d = parse_qtm_parameters_6d(xml_6d)
    xml_skeleton = xml.find("./Skeletons")
    if xml_skeleton:
        config.the_skeletons = parse_qtm_parameters_skeleton(xml_skeleton)
    return config

def parse_qtm_parameters_general(xml_general):
    frequency = None
    cameras = []
    xml_cameras = xml_general.findall("./Camera")
    for xml_camera in xml_cameras:
        camera = {}
        for xml_camera_param in xml_camera:
            tag = xml_camera_param.tag.lower()
            if tag in ["id", "model", "serial", "mode", "video_frequency"]:
                camera[tag] = xml_camera_param.text
            elif tag == "position":
                position = {}
                for xml_camera_pos_param in xml_camera_param:
                    tag = xml_camera_pos_param.tag.lower()
                    if tag in ["x", "y", "z"]:
                        position[tag] = float(xml_camera_pos_param.text)
                camera["position"] = position
        cameras.append(camera)
    frequency = float(xml_general.findtext("./Frequency"))
    return {
        "frequency": frequency,
        "cameras": cameras,
    }

def parse_qtm_parameters_3d(xml_3d):
    xml_label_names = xml_3d.findall("./Label/Name")
    markers = []
    for xml_name in xml_label_names:
        markers.append(xml_name.text)
    return {
        "markers": markers,
    }

def parse_qtm_parameters_6d(xml_6d):
    bodies = []
    xml_bodies = xml_6d.findall("./Body")
    for xml_body in xml_bodies:
        body = {
            "points": []
        }
        for xml_body_param in xml_body:
            tag = xml_body_param.tag.lower()
            if tag in ["name"]:
                body[tag] = xml_body_param.text
            elif tag == "point":
                point = {}
                for xml_point_param in xml_body_param:
                    tag = xml_point_param.tag.lower()
                    if tag in ["x", "y", "z"]:
                        point[tag] = float(xml_point_param.text)
                body["points"].append(point)
        bodies.append(body)
    xml_euler = xml_6d.find("./Euler")
    euler = {}
    for xml_euler_param in xml_euler:
        tag = xml_euler_param.tag.lower()
        if tag in ["first", "second", "third"]:
            euler[tag] = xml_euler_param.text
    return {
        "bodies": bodies,
        "euler": euler,
    }

def parse_qtm_parameters_6d_alternative(xml_6d):
    bodies = []
    xml_bodies = xml_6d.findall("./Body")
    for xml_body in xml_bodies:
        mesh = {"coordinates": []}
        body = {"points": []}
        origin = {"coordinates": []}
        for xml_body_param in xml_body:
            tag = xml_body_param.tag.lower()
            if tag in ["name"]:
                body[tag] = xml_body_param.text
            elif tag == "mesh":#Mesh is for CAD meshes that can be added (obj format)
                mesh_coord = {}
                for xml_mesh_param in xml_body_param:
                    tag = xml_mesh_param.tag.lower()
                    if tag in ["x", "y", "z"]:
                        mesh_coord[tag] = float(xml_mesh_param.text)
                mesh["coordinates"].append(mesh_coord)
            elif tag == "point":
                point = {}
                for xml_point_param in xml_body_param:
                    tag = xml_point_param.tag.lower()
                    if tag in ["x", "y", "z"]:
                        point[tag] = float(xml_point_param.text)
                body["points"].append(point)
            elif tag == "data_origin":
                point_origin = {}
                for xml_origin_param in xml_body_param:
                    tag = xml_origin_param.tag.lower()
                    if tag in ["x", "y", "z"]:
                        point_origin[tag] = float(xml_origin_param.text)
                origin["coordinates"].append(point_origin)
            #elif tag == "data_orientation":
                #Not working because uses rotation matrix? Should use convert_rot_to_eul
                # Also, not necessary because we use 6D euler data format, which contains orientation already
        bodies.append(origin)#To add the other data points, change here
    xml_euler = xml_6d.find("./Euler")
    euler = {}
    for xml_euler_param in xml_euler:
        tag = xml_euler_param.tag.lower()
        if tag in ["first", "second", "third"]:
            euler[tag] = xml_euler_param.text
    return {"bodies": bodies, "euler": euler,}

def parse_qtm_parameters_skeleton(xml_skeletons):
    skeletons = {}
    xml_skeletons_l = xml_skeletons.findall("./Skeleton")
    for xml_skeleton in xml_skeletons_l:
        #print('Data of skeleton named : '+xml_skeleton.get('Name'))
        #print(ET.tostring(xml_skeleton).decode())
        skeleton = {}#{"segments" : []}
        for xml_skeleton_segs in xml_skeleton:
            #print(ET.tostring(xml_skeleton_segs).decode())
            for xml_seg_param in xml_skeleton_segs:
                if xml_seg_param.tag.lower() == 'position':
                    positions_dic = xml_seg_param.attrib
                elif xml_seg_param.tag.lower() == 'rotation':
                    temp_dic = xml_seg_param.attrib
                    for key in temp_dic.keys():
                        key = 'q'+key
                    rotations_dic = temp_dic
                #print(ET.tostring(xml_seg_param).decode())
            skeleton[xml_skeleton_segs.get('Name')] = {**positions_dic, **rotations_dic}
            # print(skeleton)
    skeletons[xml_skeleton.get('Name')] = skeleton
    return skeletons

def mm_to_m(mm):
    return round(mm/1000, 6)

# Note: 
# Changes in channel metadata should be reflected in qtm_packet_to_lsl_sample,
# and vice versa. 

def qtm_packet_to_lsl_sample(packet):
    """
    Packet is a QRT type
    THe output of the funcion is a list of list containing markers, bodies, and skeletons (initialized as empty)
    """
    sample = [[],[],[]]
    if QRTComponentType.Component3d in packet.components:
        _, markers = packet.get_3d_markers()
        for marker in markers:
            marker_data = []
            marker_data.append(mm_to_m(marker.x))
            marker_data.append(mm_to_m(marker.y))
            marker_data.append(mm_to_m(marker.z))
            sample[0].extend(marker_data)
    elif QRTComponentType.Component6dEuler in packet.components:
        _, bodies = packet.get_6d_euler()
        for position, rotation in bodies:
            body_data = []
            body_data.append(mm_to_m(position.x))
            body_data.append(mm_to_m(position.y))
            body_data.append(mm_to_m(position.z))
            body_data.append(rotation.a1)
            body_data.append(rotation.a2)
            body_data.append(rotation.a3)
            sample[1].extend(body_data)
    elif QRTComponentType.ComponentSkeleton in packet.components:
        unknown, skeletons = packet.get_skeletons()
        for skeleton in skeletons:
            if len(skeleton)==0:
                print('The skeleton streamed does not contain data, remove it from QTM')
            else:
                skeleton_data = []
                counter=0
                #print('The skeleton is :' + str(skeleton))
                for segment_id, position, rotation in skeleton:
                    segment_data = []
                    # segment_data.append(segment_id)
                    segment_data.append(position.x)#mm_to_m(position.x))
                    segment_data.append(position.y)
                    segment_data.append(position.z)
                    segment_data.append(rotation.x)
                    segment_data.append(rotation.y)
                    segment_data.append(rotation.z)
                    segment_data.append(rotation.w)
                    #skeleton_data.append(segment_data)
                    skeleton_data.extend(segment_data)
                    counter+=7
                sample[2].append(skeleton_data)
                # print("{0} values added to Skeleton".format(counter))
    # print('The sample is : ' + str(sample))
    return sample

def new_lsl_stream_info(config, qtm_host, qtm_port, content, name=""):
    if content is "skeletons":
        stream_name = "Qualisys skeleton - " + name
    elif content is "markers":
        stream_name = "Qualisys markers"
    elif content is "bodies":
        stream_name = "Qualisys rigid bodies"
    info = StreamInfo(
        name=stream_name,
        type="Mocap",
        channel_count=config.channel_count(),
        nominal_srate=config.general['frequency'],
        channel_format=cf_float32,
        source_id="{}:{}".format(qtm_host, qtm_port),
    )

    channels = info.desc().append_child("channels")
    check_counter = 0
    if content is 'markers':
        for marker in config.markers():
            markers_name = ['_x', '_y', '_z']
            for i in range(3):
                check_counter += 1
                channels.append_child_value('label', str(marker)+markers_name[i])
                channels.append_child_value('unit', 'mm')
                channels.append_child_value('precision', '1')
    if content is 'bodies':
        for body in config.bodies():
            bodies_name = ['_x', '_y', '_z', '_roll', '_pitch', '_yaw']
            bodies_units = ['mm', 'mm', 'mm', '°', '°', '°']
            for i in range(len(bodies_name)):
                check_counter += 1
                channels.append_child_value('label', str(body["name"])+bodies_name[i])
                channels.append_child_value('unit', bodies_units[i])
                channels.append_child_value('precision', '1')
    if content is 'skeletons':
        for key, val in config.skeleton().items():
            for seg_names, seg_values in val.items():
                segments_name = ['x', 'y', 'z', 'qx', 'qy', 'qz', 'qw']
                segments_units = ['mm', 'mm', 'mm', '', '', '', '']
                for i in range(len(segments_name)):
                    check_counter += 1
                    current_channel = channels.append_child('Ch'+str(check_counter))
                    current_channel.append_child_value('label', '{0}_{1}_{2}'.format(str(key), str(seg_names), segments_name[i]))
                    #print('{0}_{1}_{2}'.format(str(key), str(seg_names), segments_name[i]))
                    current_channel.append_child_value('unit', segments_units[i])
                    current_channel.append_child_value('precision', '1')
    hardware = info.desc().append_child("hardware")
    hardware.append_child_value("manufacturer", 'Qualisys')
    hardware.append_child_value("model", 'ArqusA5')
    hardware.append_child_value("serial", '')
    hardware.append_child_value("config", '')
    hardware.append_child_value("location", '')
    sync = info.desc().append_child("synchronization")
    sync.append_child_value("time_source", 'Mod0')
    sync.append_child_value("offset_mean", '0')
    sync.append_child_value("can_drop_samples", 'False')
    sync.append_child_value("inlet_processing_options", 'Clocksync')
    sync.append_child_value("outlet_processing_options", 'None')
    sync.append_child_value("outlet_drift_coeffificent", '0')
    sync.append_child_value("outlet_jitter_coeffificent", '0')
    cameras = info.desc().append_child("cameras")
    lsl_stream_info_add_cameras(config, cameras)
    # print('The amount of channels in the metadata is : ' + str(check_counter))
    print('The channel count in the info is : ' + str(config.channel_count()))
    '''
    channels = info.desc().append_child("channels")
    setup = info.desc().append_child("setup")
    if content is "markers":
        markers = setup.append_child("markers")
        lsl_stream_info_add_markers(config, channels, markers)
    elif content is "bodies":
        objects = setup.append_child("objects")
        lsl_stream_info_add_6dof(config, channels, objects)
    elif content is "skeletons":
        skeleton = setup.append_child("skeleton")
        lsl_stream_info_add_skeletons(config, channels, skeleton)
    cameras = setup.append_child("cameras")
    lsl_stream_info_add_cameras(config, cameras)
    info.desc().append_child("acquisition") \
        .append_child_value("manufacturer", "Qualisys") \
        .append_child_value("model", "Qualisys Track Manager")
    '''
    return info

def lsl_stream_info_add_markers(config, channels, markers):
    def append_channel(marker, component, ch_type, unit):
        label = "{}_{}".format(marker, component)
        channels.append_child("channel") \
            .append_child_value("label", label) \
            .append_child_value("marker", marker) \
            .append_child_value("type", ch_type) \
            .append_child_value("unit", unit)
    def append_position_channel(marker, component):
        ch_type = "Position" + component
        append_channel(marker, component, ch_type, "meters")
    for marker in config.markers():
        markers.append_child("marker") \
            .append_child_value("label", marker)
        append_position_channel(marker, "X")
        append_position_channel(marker, "Y")
        append_position_channel(marker, "Z")

def lsl_stream_info_add_6dof(config, channels, objects):
    def append_channel(body, component, ch_type, unit):
        label = "{}_{}".format(body, component)
        channels.append_child("channel") \
            .append_child_value("label", label) \
            .append_child_value("object", body) \
            .append_child_value("type", ch_type) \
            .append_child_value("unit", unit)
    euler_angle_to_component = {
        "pitch": "P",
        "roll": "R",
        "yaw": "H",
    }
    def append_position_channel(body, component):
        ch_type = "Position" + component
        append_channel(body, component, ch_type, "meters")
    def append_orientation_channel(body, angle):
        component = euler_angle_to_component[angle.lower()]
        ch_type = "Orientation" + component
        append_channel(body, component, ch_type, "degrees")
    for body in config.bodies():
        name = body["name"]
        objects.append_child("object") \
            .append_child_value("class", "Rigid") \
            .append_child_value("label", name)
        append_position_channel(name, "X")
        append_position_channel(name, "Y")
        append_position_channel(name, "Z")
        angles = config.the_6d["euler"]
        append_orientation_channel(name, angles["first"])
        append_orientation_channel(name, angles["second"])
        append_orientation_channel(name, angles["third"])

def lsl_stream_info_add_skeletons(config, channels, skeletons):
    def append_channel(segment, component, ch_type, unit):
        label = "{}_{}".format(segment, component)
        channels.append_child("channel") \
            .append_child_value("label", label) \
            .append_child_value("segment", segment) \
            .append_child_value("type", ch_type) \
            .append_child_value("unit", unit) \
            .append_child_value('precision', '3')
    def append_position_channel(segment, component):
        ch_type = "Position" + component
        append_channel(segment, component, ch_type, "mm")
    def append_orientation_channel(segment, angle):
        component = angle.lower()
        ch_type = "Orientation" + component
        append_channel(segment, component, ch_type, "quaternions")
    #If several skeletons are given if only one by one (one/stream, I suppose)
    for key, val in config.skeleton().items():
        lsl_skeleton = skeletons.append_child("object") \
            .append_child_value("class", "Skeleton") \
            .append_child_value("label", key)
        channel_counter = 0
        for seg_names, seg_values in val.items():
    
            #for segment in config.skeletons():
            name = key + str(seg_names)
            lsl_skeleton.append_child("channels") \
                .append_child_value("class", "Segment") \
                .append_child_value("label", name)
            append_position_channel(name, "X")
            append_position_channel(name, "Y")
            append_position_channel(name, "Z")
            append_orientation_channel(name, "QX")
            append_orientation_channel(name, "QY")
            append_orientation_channel(name, "QZ")
            append_orientation_channel(name, "QW")
        lsl_skeleton.append_child('hardware')\
            .append_child_value("manufacturer", 'Qualisys')\
            .append_child_value("model", 'ArqusA5')

def lsl_stream_info_add_cameras(config, cameras):
    def fmt_pos(pos):
        return str(mm_to_m(pos))
    for camera in config.cameras():
        info = cameras.append_child("cameras") \
            .append_child_value("label", camera["id"])
        if "position" in camera:
            pos = camera["position"]
            info.append_child("position") \
                .append_child_value("X", fmt_pos(pos["x"])) \
                .append_child_value("Y", fmt_pos(pos["y"])) \
                .append_child_value("Z", fmt_pos(pos["z"]))

def convert_rot_to_eul(R):#uses numpy data format, and might not work due to rotation matrix
    sy = math.sqrt(R[0,0] * R[0,0] +  R[1,0] * R[1,0])
    singular = sy < 1e-6
    if  not singular :
        x = math.atan2(R[2,1] , R[2,2])
        y = math.atan2(-R[2,0], sy)
        z = math.atan2(R[1,0], R[0,0])
    else :
        x = math.atan2(-R[1,2], R[1,1])
        y = math.atan2(-R[2,0], sy)
        z = 0
    return np.array([x, y, z])

