# Trend Checkpoints

AT = matched_analyzed_result
DT = had_no_trend
RT = database trend

|AT |DT |RT |Storage    |Login                    |Search        |Job           |
|---|---|---|-----------|-------------------------|--------------|------------- |
| X | X | X |           |If finished = Go: Others |              |Go: next      |
| X | X |   |           |Close                    |Close         |Close         |
|   | X |   |           |                         |              |              |
| X |   |   |Create new |If finished = Go: Others |              |Go: next      |
|   |   |   |Reserve    |                         |Open: search  |Open: job     |
