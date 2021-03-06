﻿using FFmpeg.AutoGen;
using System;
using System.Drawing;
using System.IO;

namespace FFmpeg_usbCam.FFmpeg
{
    public unsafe class H264VideoStreamEncoder : IDisposable
    {
        int enc_stream_index;
        AVFormatContext* oFormatContext;
        AVCodecContext* oCodecContext;
        
        public void OpenOutputURL(string fileName, EncodingInfo enCodecInfo)
        {
            AVStream* out_stream;
            AVCodec* encoder;

            int ret;

            //output file
            var _oFormatContext = oFormatContext;

            ffmpeg.avformat_alloc_output_context2(&_oFormatContext, null, null, fileName);

            encoder = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_H264);

            out_stream = ffmpeg.avformat_new_stream(_oFormatContext, encoder);

            oCodecContext = ffmpeg.avcodec_alloc_context3(encoder);
            oCodecContext = out_stream->codec;

            oCodecContext->height = enCodecInfo.Height;
            oCodecContext->width = enCodecInfo.Width;
            oCodecContext->sample_aspect_ratio = enCodecInfo.Sample_aspect_ratio;
            oCodecContext->pix_fmt = encoder->pix_fmts[0];
            oCodecContext->time_base = enCodecInfo.Timebase;
            oCodecContext->framerate = ffmpeg.av_inv_q(enCodecInfo.Framerate);

            if ((_oFormatContext->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
            {
                oCodecContext->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
            }

            //open codecd
            ret = ffmpeg.avcodec_open2(oCodecContext, encoder, null).ThrowExceptionIfError();

            ret = ffmpeg.avcodec_parameters_from_context(out_stream->codecpar, oCodecContext);
            out_stream->time_base = oCodecContext->time_base;

            //Show some Information
            ffmpeg.av_dump_format(_oFormatContext, 0, fileName, 1);

            if (ffmpeg.avio_open(&_oFormatContext->pb, fileName, ffmpeg.AVIO_FLAG_WRITE) < 0)
            {
                Console.WriteLine("Failed to open output file! \n");
            }

            //Write File Header
            int error = ffmpeg.avformat_write_header(_oFormatContext, null);
            error.ThrowExceptionIfError();

            oFormatContext = _oFormatContext;
        }

        public void Dispose()
        {
            var _oFormatContext = oFormatContext;

            //Write file trailer
            ffmpeg.av_write_trailer(_oFormatContext);

            ffmpeg.avformat_close_input(&_oFormatContext);
        }

        public void TryEncodeNextPacket(AVFrame uncompressed_frame)
        {
            int ret;
            AVPacket* encoded_packet;

            encoded_packet = ffmpeg.av_packet_alloc();
            ffmpeg.av_init_packet(encoded_packet);

            //Supply a raw video frame to the output condec context
            ret = ffmpeg.avcodec_send_frame(oCodecContext, &uncompressed_frame);
            ret.ThrowExceptionIfError();

            while (true)
            {
                //read encodeded packet from output codec context
                ret = ffmpeg.avcodec_receive_packet(oCodecContext, encoded_packet);

                /* if no more frames for output - returns AVERROR(EAGAIN)
                * if flushed and no more frames for output - returns AVERROR_EOF
                * rewrite retcode to 0 to show it as normal procedure completion
                */
                if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN) || ret == ffmpeg.AVERROR(ffmpeg.AVERROR_EOF))
                {
                    break;
                }
                else if (ret < 0)
                {
                    Console.WriteLine("Error during encoding \n");
                    break;
                }

                enc_stream_index = encoded_packet->stream_index;

                //set packet pts & dts for timestamp
                if (encoded_packet->pts != ffmpeg.AV_NOPTS_VALUE)
                    encoded_packet->pts = (long)(ffmpeg.av_rescale_q(encoded_packet->pts, oCodecContext->time_base, oFormatContext->streams[enc_stream_index]->time_base));
                if (encoded_packet->dts != ffmpeg.AV_NOPTS_VALUE)
                    encoded_packet->dts = ffmpeg.av_rescale_q(encoded_packet->dts, oCodecContext->time_base, oFormatContext->streams[enc_stream_index]->time_base);

                //write frame in video file
                ffmpeg.av_write_frame(oFormatContext, encoded_packet);
            }
        }

        public void FlushEncode()
        {
            ffmpeg.avcodec_send_frame(oCodecContext, null);
        }


    }
}
