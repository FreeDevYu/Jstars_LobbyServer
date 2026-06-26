-- MatchingQueue ZSET: score-ordered win-rate queue.
-- Finds n consecutive players whose score spread is within range, ZREM on match.
-- Params: @queueKey, @n, @range, @limit (StackExchange.Redis LuaScript)

local playerCount = tonumber(@n)
local scoreRange = tonumber(@range)
local scanLimit = tonumber(@limit)

local players = redis.call('ZRANGE', @queueKey, 0, scanLimit, 'WITHSCORES')
if #players < (playerCount * 2) then
    return nil
end

local entryCount = #players / 2
local lastStartRank = entryCount - playerCount + 1

for startRank = 1, lastStartRank do
    local minScore = tonumber(players[(startRank - 1) * 2 + 2])
    local maxScore = tonumber(players[(startRank + playerCount - 1 - 1) * 2 + 2])

    if (maxScore - minScore) <= scoreRange then
        local matchedUids = {}
        for offset = 0, playerCount - 1 do
            local uid = players[(startRank + offset - 1) * 2 + 1]
            table.insert(matchedUids, uid)
            redis.call('ZREM', @queueKey, uid)
        end
        return matchedUids
    end
end

return nil
