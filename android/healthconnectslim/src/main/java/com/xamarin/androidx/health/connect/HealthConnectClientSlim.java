package com.xamarin.androidx.health.connect;

import android.content.Context;
import androidx.health.connect.client.*;
import androidx.health.connect.client.aggregate.AggregationResult;
import androidx.health.connect.client.records.HeartRateRecord;
import androidx.health.connect.client.records.Record;
import androidx.health.connect.client.request.AggregateRequest;
import androidx.health.connect.client.request.ReadRecordsRequest;
import androidx.health.connect.client.time.TimeRangeFilter;
import androidx.health.platform.client.proto.AbstractMessageLite;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.util.List;

public class HealthConnectClientSlim {
    final HealthConnectClient client;

    public HealthConnectClientSlim(Context context) {
        this.client = HealthConnectClient.getOrCreate(context);
//        this.client.readRecords()
    }

    public AggregationResult Aggregate(AggregateRequest request) {
        return null;
    }

    public List<HeartRateRecord> readHeartRate() {
        this.client.readRecords()
        return null;
    }

    public void readRecords(LocalDateTime startTime, LocalDateTime endTime) {
//        var filter = TimeRangeFilter.between(startTime, endTime);
//        new Set<>
//        var request = new ReadRecordsRequest<HeartRateRecord>(
//            filter
//        );

//        AggregationResult result = this.client.aggregate(
//             {}

//                    setOf(HeartRateRecord.BPM_MAX, HeartRateRecord.BPM_MIN),
//                    timeRangeFilter = TimeRangeFilter.between(startTime, endTime)
//            )
//        );
        // The result may be null if no data is available in the time range
//        var minimumHeartRate = response[HeartRateRecord.BPM_MIN]
//        val maximumHeartRate = response[HeartRateRecord.BPM_MAX]
//        this.client.readRecords(new ReadRecordsRequest<Record>())
    }
}
